using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using zionet_workflow.Configuration;
using zionet_workflow.Evals;
using zionet_workflow.Executors;
using zionet_workflow.Models;
using zionet_workflow.SampleData;
using zionet_workflow.Services;
using zionet_workflow.Workflows;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var geminiConfig = GeminiConfiguration.LoadFromConfiguration(configuration);

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger("ApplicationTriage");

logger.LogInformation("╔═══════════════════════════════════════════════════════════════╗");
logger.LogInformation("║          AI ENGINEERING - JOB APPLICATION TRIAGE SYSTEM       ║");
logger.LogInformation("║  Microsoft Agent Framework (MAF) + Google Gemini Integration  ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════════╝");
logger.LogInformation("");

try
{
    var classificationAgent = new GeminiClassificationAgent(
        geminiConfig,
        loggerFactory.CreateLogger<GeminiClassificationAgent>());

    var classifier = new ClassifierExecutor(
        classificationAgent,
        loggerFactory.CreateLogger<ClassifierExecutor>());

    var router = new RouterExecutor(loggerFactory.CreateLogger<RouterExecutor>());
    var approvalService = new HumanApprovalService(loggerFactory.CreateLogger<HumanApprovalService>());

    var auditLogPath = configuration["AuditLog:Path"] ?? "audit.jsonl";
    var auditLogger = new AuditLogger(auditLogPath, loggerFactory.CreateLogger<AuditLogger>());

    var workflow = new ApplicationTriageWorkflow(logger, classifier, router, approvalService, auditLogger);

    // ─── Main workflow run ────────────────────────────────────────────────────
    var applications = SampleApplications.GetAllSamples();

    foreach (var application in applications)
    {
        logger.LogInformation("");
        logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
        logger.LogInformation("║ PROCESSING: {Id} ({Name})",
            application.Id, application.ApplicantName);
        logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");

        await foreach (var @event in workflow.RunAsync(application))
        {
            DisplayEvent(@event);
        }

        logger.LogInformation("");
        await Task.Delay(500);
    }

    logger.LogInformation("All applications processed.");

    // ─── Evaluation suite ─────────────────────────────────────────────────────
    logger.LogInformation("");
    logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
    logger.LogInformation("║  RUNNING EVALUATION SUITE ({Count} labeled cases)              ║",
        EvalRunner.Cases.Count);
    logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");

    var evalRunner = new EvalRunner(classifier, loggerFactory.CreateLogger<EvalRunner>());
    var evalResults = await evalRunner.RunAsync();
    EvalRunner.PrintReport(evalResults);
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error during workflow execution");
    Environment.Exit(1);
}

static void DisplayEvent(WorkflowEvent @event)
{
    var prefix = @event.Type switch
    {
        WorkflowEventType.ExecutorStarted => "→ START",
        WorkflowEventType.ExecutorCompleted => "✓ DONE ",
        WorkflowEventType.Output => "  RESULT",
        _ => "?      ",
    };

    Console.WriteLine($"  [{prefix}] {@event.ExecutorId.ToUpper()}: {@event.Timestamp:HH:mm:ss.fff}");

    if (@event.Data != null && @event.Type == WorkflowEventType.ExecutorCompleted)
    {
        var truncated = @event.Data.Length > 120
            ? @event.Data[..120] + "..."
            : @event.Data;
        Console.WriteLine($"         {truncated}");
    }
}
