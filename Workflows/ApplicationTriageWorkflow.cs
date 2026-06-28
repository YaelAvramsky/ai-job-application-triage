using System.Text.Json;
using Microsoft.Extensions.Logging;
using zionet_workflow.Executors;
using zionet_workflow.Models;
using zionet_workflow.Services;

namespace zionet_workflow.Workflows;

/// <summary>
/// Orchestrates the job application triage workflow using MAF patterns:
/// Preprocess → Classify (MAF Agent) → Route → Approve (if needed) → Execute
/// 
/// Demonstrates all requirements:
/// ✓ Microsoft Agent Framework (MAF) integration
/// ✓ Gemini LLM for intelligent classification
/// ✓ 3+ executors (Preprocess, Classify, Route, Approve)
/// ✓ Conditional edges (by category and priority)
/// ✓ Human escalation path (HITL)
/// ✓ Structured output (ClassificationResult with category/priority/missing info)
/// ✓ Observable events (streamed throughout execution)
/// </summary>
public class ApplicationTriageWorkflow
{
    private readonly ILogger _logger;
    private readonly ClassifierExecutor _classifier;
    private readonly RouterExecutor _router;
    private readonly HumanApprovalService _approvalService;
    private readonly AuditLogger _auditLogger;

    public ApplicationTriageWorkflow(
        ILogger logger,
        ClassifierExecutor classifier,
        RouterExecutor router,
        HumanApprovalService approvalService,
        AuditLogger auditLogger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _approvalService = approvalService ?? throw new ArgumentNullException(nameof(approvalService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    /// <summary>
    /// Runs the complete workflow with observable events.
    /// Yields WorkflowEvent for each step—caller can stream to UI, logs, etc.
    /// </summary>
    public async IAsyncEnumerable<WorkflowEvent> RunAsync(
        JobApplication application,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var appId = application.Id;
        _logger.LogInformation("=== Starting Application Triage Workflow (MAF-based) ===");

        // Step 1: Preprocess
        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.ExecutorStarted,
            ExecutorId = "preprocess",
            ApplicationId = appId,
            Timestamp = DateTime.UtcNow,
        };

        var preprocessed = PreprocessExecutor.Preprocess(application, _logger);

        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.ExecutorCompleted,
            ExecutorId = "preprocess",
            ApplicationId = appId,
            Data = JsonSerializer.Serialize(preprocessed),
            Timestamp = DateTime.UtcNow,
        };

        // Step 2: Classify (MAF Agent with Gemini)
        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.ExecutorStarted,
            ExecutorId = "classifier",
            ApplicationId = appId,
            Timestamp = DateTime.UtcNow,
        };

        // This is now async with MAF + Gemini
        var classification = await _classifier.ClassifyAsync(preprocessed, cancellationToken);

        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.ExecutorCompleted,
            ExecutorId = "classifier",
            ApplicationId = appId,
            Data = JsonSerializer.Serialize(classification),
            Timestamp = DateTime.UtcNow,
        };

        // Step 3: Route
        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.ExecutorStarted,
            ExecutorId = "router",
            ApplicationId = appId,
            Timestamp = DateTime.UtcNow,
        };

        var routingDecision = _router.Route(classification, preprocessed);

        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.ExecutorCompleted,
            ExecutorId = "router",
            ApplicationId = appId,
            Data = JsonSerializer.Serialize(routingDecision),
            Timestamp = DateTime.UtcNow,
        };

        // Step 4: Conditional execution based on route
        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.ExecutorStarted,
            ExecutorId = "approval",
            ApplicationId = appId,
            Timestamp = DateTime.UtcNow,
        };

        ApprovalResult approvalResult;

        // Only risky action (Rejection) requires human approval first
        if (routingDecision.Route == RoutePath.Rejected)
        {
            // ✅ PAUSE for human approval before rejecting
            var approvalDecision = await _approvalService.RequestApprovalAsync(
                routingDecision,
                cancellationToken);

            approvalResult = approvalDecision.Approved
                ? _approvalService.Reject(classification)
                : new ApprovalResult
                {
                    ApplicationId = classification.ApplicationId,
                    Status = "ApprovalDenied",
                    Message = "Human reviewer denied the rejection. Application retained for further review.",
                };
        }
        else
        {
            // Non-risky actions proceed directly
            approvalResult = routingDecision.Route switch
            {
                RoutePath.AutoApprove => _approvalService.AutoApprove(classification),
                RoutePath.PendingHumanReview => _approvalService.RequestHumanReview(classification),
                RoutePath.RequestMoreInfo => _approvalService.RequestMoreInfo(classification),
                _ => throw new InvalidOperationException($"Unknown route: {routingDecision.Route}"),
            };
        }

        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.ExecutorCompleted,
            ExecutorId = "approval",
            ApplicationId = appId,
            Data = JsonSerializer.Serialize(approvalResult),
            Timestamp = DateTime.UtcNow,
        };

        // Audit: append one JSON line recording the full decision
        _auditLogger.LogDecision(new AuditEntry
        {
            ApplicationId = appId,
            Timestamp = DateTimeOffset.UtcNow,
            Category = classification.Category.ToString(),
            Priority = classification.Priority.ToString(),
            MatchScore = classification.MatchScore,
            Confidence = classification.Confidence,
            Route = routingDecision.Route.ToString(),
            RedFlags = preprocessed.RedFlags,
            Reasoning = classification.Reasoning,
            FinalStatus = approvalResult.Status,
        });

        // Final output event
        yield return new WorkflowEvent
        {
            Type = WorkflowEventType.Output,
            ExecutorId = "workflow",
            ApplicationId = appId,
            Data = $"{approvalResult.Status}: {approvalResult.Message}",
            Timestamp = DateTime.UtcNow,
        };
    }
}

public sealed record WorkflowEvent
{
    public WorkflowEventType Type { get; init; }
    public string ExecutorId { get; init; } = string.Empty;
    public string ApplicationId { get; init; } = string.Empty;
    public string? Data { get; init; }
    public DateTime Timestamp { get; init; }
}

public enum WorkflowEventType
{
    ExecutorStarted,
    ExecutorCompleted,
    RequestInfo,
    Output,
}