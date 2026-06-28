using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Logging;
using zionet_workflow.Configuration;
using zionet_workflow.Executors;
using zionet_workflow.Models;
using zionet_workflow.SampleData;
using zionet_workflow.Services;
using zionet_workflow.Workflows;

// Build configuration (respects precedence: env vars > user secrets > appsettings.json)
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// Load and validate Gemini configuration
var geminiConfig = GeminiConfiguration.LoadFromConfiguration(configuration);

// Initialize logging
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
    // Initialize services and executors with MAF agent
    var classificationAgent = new GeminiClassificationAgent(
        geminiConfig,
        loggerFactory.CreateLogger<GeminiClassificationAgent>());

    var classifier = new ClassifierExecutor(
        classificationAgent,
        loggerFactory.CreateLogger<ClassifierExecutor>());

    var router = new RouterExecutor(loggerFactory.CreateLogger<RouterExecutor>());
    var approvalService = new HumanApprovalService(loggerFactory.CreateLogger<HumanApprovalService>());

    // Create workflow
    var workflow = new ApplicationTriageWorkflow(
        logger,
        classifier,
        router,
        approvalService);

    // Run workflow on all sample applications with observable events
    var applications = SampleApplications.GetAllSamples();

    foreach (var application in applications)
    {
        logger.LogInformation("");
        logger.LogInformation("╔════════════════════════════════════════════════════════════════╗");
        logger.LogInformation("║ PROCESSING APPLICATION: {Id} ({Name})",
            application.Id, application.ApplicantName);
        logger.LogInformation("╚════════════════════════════════════════════════════════════════╝");

        // Stream events and display each step
        await foreach (var @event in workflow.RunAsync(application))
        {
            DisplayEvent(@event);
        }

        logger.LogInformation("");
        await Task.Delay(500); // Brief pause between applications
    }

    logger.LogInformation("✓ All applications processed successfully.");
}
catch (Exception ex)
{
    logger.LogError(ex, "✗ Fatal error during workflow execution");
    Environment.Exit(1);
}

static void DisplayEvent(WorkflowEvent @event)
{
    var prefix = @event.Type switch
    {
        WorkflowEventType.ExecutorStarted => "→ START",
        WorkflowEventType.ExecutorCompleted => "✓ DONE",
        WorkflowEventType.Output => "📤 RESULT",
        _ => "?",
    };

    Console.WriteLine($"  [{prefix}] {@event.ExecutorId.ToUpper()}: {@event.Timestamp:HH:mm:ss.fff}");

    if (@event.Data != null && @event.Type == WorkflowEventType.ExecutorCompleted)
    {
        var truncated = @event.Data.Length > 100
            ? @event.Data[..100] + "..."
            : @event.Data;
        Console.WriteLine($"       {truncated}");
    }
}
