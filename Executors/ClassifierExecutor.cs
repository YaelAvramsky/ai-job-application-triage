using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using zionet_workflow.Models;
using zionet_workflow.Services;

namespace zionet_workflow.Executors;

/// <summary>
/// Agent-based executor node using Microsoft Agent Framework (MAF).
/// 
/// Demonstrates MAF ChatAgent pattern with:
/// - Gemini LLM for intelligent classification
/// - Structured output parsing
/// - Enterprise error handling and observability
/// </summary>
public class ClassifierExecutor
{
    private readonly GeminiClassificationAgent _mafAgent;
    private readonly ILogger _logger;

    public ClassifierExecutor(GeminiClassificationAgent mafAgent, ILogger logger)
    {
        _mafAgent = mafAgent ?? throw new ArgumentNullException(nameof(mafAgent));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Classifies a preprocessed application using MAF agent with Gemini LLM.
    /// </summary>
    public async Task<ClassificationResult> ClassifyAsync(
        PreprocessedApplication app,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Classifying application {Id} via MAF Agent + Gemini LLM",
            app.Id);

        try
        {
            var result = await _mafAgent.ClassifyAsync(app, cancellationToken);

            _logger.LogInformation(
                "✓ Classified {Id}: {Category} (Priority: {Priority}, Match: {Score:P})",
                app.Id, result.Category, result.Priority, result.MatchScore);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Classification cancelled for application {Id}", app.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "✗ Failed to classify application {Id}",
                app.Id);
            throw;
        }
    }
}
