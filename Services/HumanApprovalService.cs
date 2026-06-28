using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using zionet_workflow.Models;

namespace zionet_workflow.Services;

/// <summary>
/// Service for capturing human approval/rejection in the workflow.
/// In production: integrates with email, UI, or approval queue systems.
/// </summary>
public class HumanApprovalService
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ApprovalDecision> _decisions = [];

    public HumanApprovalService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Simulates waiting for human approval (HITL pattern).
    /// In production: this integrates with your approval system.
    /// </summary>
    public async Task<ApprovalDecision> RequestApprovalAsync(
        RoutingDecision routingDecision,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Pausing for human approval: {Id}. Reason: {Reason}",
            routingDecision.ApplicationId, routingDecision.Reason);

        // Simulate waiting for human decision
        // In production: check approval queue, send notification, wait for response
        await Task.Delay(100, cancellationToken);

        var decision = new ApprovalDecision
        {
            ApplicationId = routingDecision.ApplicationId,
            Approved = true,
            ApprovedBy = "Human Reviewer",
            ApprovedAt = DateTime.UtcNow,
            Notes = "Passed manual review.",
        };

        _logger.LogInformation("Approval received: {Id} approved={Approved}",
            routingDecision.ApplicationId, decision.Approved);

        return decision;
    }

    /// <summary>
    /// Auto-approve the application and move to interview phase.
    /// </summary>
    public ApprovalResult AutoApprove(ClassificationResult classification)
    {
        _logger.LogInformation("Auto-approving application: {Id}", classification.ApplicationId);

        return new ApprovalResult
        {
            ApplicationId = classification.ApplicationId,
            Status = "Approved",
            Message = "Application automatically approved. Proceeding to interview phase.",
        };
    }

    /// <summary>
    /// Request human review of the application.
    /// </summary>
    public ApprovalResult RequestHumanReview(ClassificationResult classification)
    {
        _logger.LogWarning("Requesting human review for application: {Id}", classification.ApplicationId);

        return new ApprovalResult
        {
            ApplicationId = classification.ApplicationId,
            Status = "PendingReview",
            Message = "Application flagged for human review due to borderline classification.",
        };
    }

    /// <summary>
    /// Reject the application.
    /// </summary>
    public ApprovalResult Reject(ClassificationResult classification)
    {
        _logger.LogInformation("Rejecting application: {Id}", classification.ApplicationId);

        return new ApprovalResult
        {
            ApplicationId = classification.ApplicationId,
            Status = "Rejected",
            Message = "Application does not meet the required qualifications.",
        };
    }

    /// <summary>
    /// Request additional information from the applicant.
    /// </summary>
    public ApprovalResult RequestMoreInfo(ClassificationResult classification)
    {
        _logger.LogInformation("Requesting additional information for application: {Id}", classification.ApplicationId);

        return new ApprovalResult
        {
            ApplicationId = classification.ApplicationId,
            Status = "MoreInfoRequested",
            Message = $"Additional information requested. Missing: {string.Join(", ", classification.MissingInfo)}",
        };
    }
}

public sealed record ApprovalDecision
{
    public string ApplicationId { get; init; } = string.Empty;
    public bool Approved { get; init; }
    public string ApprovedBy { get; init; } = string.Empty;
    public DateTime ApprovedAt { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public sealed record ApprovalResult
{
    public string ApplicationId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
