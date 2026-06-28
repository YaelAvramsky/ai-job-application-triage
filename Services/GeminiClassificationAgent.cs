using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using zionet_workflow.Configuration;
using zionet_workflow.Models;

namespace zionet_workflow.Services;

/// <summary>
/// Microsoft Agent Framework (MAF) agent for job application classification.
/// Uses GeminiSchemaBuilder to derive the Gemini responseSchema directly from
/// ClassificationResult, so the schema and the C# model can never drift apart.
/// </summary>
public class GeminiClassificationAgent
{
    private readonly IChatClient _chatClient;
    private readonly ILogger _logger;
    private readonly string _modelId;

    public GeminiClassificationAgent(GeminiConfiguration config, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelId = config.ModelId;

        try
        {
            var schema = GeminiSchemaBuilder.BuildFrom<ClassificationResult>();

            _chatClient = new GeminiChatClientAdapter(config.ApiKey, config.ModelId, schema);

            _logger.LogInformation(
                "Initialized GeminiClassificationAgent (Model: {ModelId}, schema derived from {Type})",
                _modelId, nameof(ClassificationResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GeminiClassificationAgent");
            throw;
        }
    }

    public async Task<ClassificationResult> ClassifyAsync(
        PreprocessedApplication app,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Classifying application {Id} via MAF Agent + Gemini", app.Id);

        try
        {
            var prompt = BuildClassificationPrompt(app);

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.User, prompt)
            };

            var response = await _chatClient.GetResponseAsync(
                messages,
                cancellationToken: cancellationToken);

            var responseText = response.Text
                ?? throw new InvalidOperationException("Empty response from Gemini");

            var classification = ParseStructuredOutput(responseText, app.Id);

            _logger.LogInformation(
                "Classified {Id}: {Category} (Priority: {Priority}, Match: {Score:P0}, Confidence: {Conf:P0})",
                app.Id, classification.Category, classification.Priority,
                classification.MatchScore, classification.Confidence);

            return classification;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Classification cancelled for application {Id}", app.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during classification of application {Id}", app.Id);
            throw;
        }
    }

    private static string BuildClassificationPrompt(PreprocessedApplication app)
    {
        return $$"""
            You are an expert HR recruiter evaluating job applications using structured criteria.
            Classify this application and report how confident you are in that classification.

            ═══════════════════════════════════════════════════════════════════════════════
            APPLICANT PROFILE:
            ═══════════════════════════════════════════════════════════════════════════════
            Application ID: {{app.Id}}
            Position Applied: {{app.Position}}
            Years of Experience: {{app.YearsOfExperience}}
            Has Required Skills (C#, SQL, Azure): {{(app.HasRequiredSkills ? "YES" : "NO")}}
            Cover Letter Length: {{app.CoverLetterLength}} characters
            Skills: {{string.Join(", ", app.Skills)}}

            ═══════════════════════════════════════════════════════════════════════════════
            EVALUATION CRITERIA:
            ═══════════════════════════════════════════════════════════════════════════════

            EXPERIENCE ASSESSMENT:
            • 5+ years = Excellent (0.3 points toward matchScore)
            • 3-4 years = Good (0.2 points)
            • 1-2 years = Acceptable (0.1 points)
            • <1 year = Minimal (0 points)

            SKILLS MATCH:
            • Has required skills = Critical match (0.4 points)
            • Missing skills = Partial credit (0.1 points)

            COVER LETTER QUALITY:
            • >300 characters = Excellent effort (0.3 points)
            • 100-300 characters = Adequate (0.15 points)
            • <100 characters = Minimal effort (0 points)

            CATEGORY DEFINITIONS (mutually exclusive):
            • StrongCandidate: matchScore ≥ 0.8 AND has required skills → ready for immediate interview
            • FitCandidate:    matchScore ≥ 0.5 AND < 0.8              → meets baseline, proceed to next round
            • WeakCandidate:   matchScore < 0.5                        → below expectations
            • Incomplete:      critical information is missing          → request clarification

            PRIORITY DEFINITIONS:
            • High:   StrongCandidate or exceptional fit
            • Medium: FitCandidate with good potential
            • Low:    WeakCandidate or pending information

            CONFIDENCE DEFINITION:
            Rate your confidence in the classification itself, not the candidate's fit:
            • 0.9-1.0: The evidence clearly supports one category with no ambiguity.
            • 0.7-0.9: The category is likely correct but the application has minor gaps.
            • 0.5-0.7: The application is borderline; another category is plausible.
            • < 0.5:   Very little usable information; classification is a best guess.

            Evaluate the application and provide your structured assessment.
            """;
    }

    private static ClassificationResult ParseStructuredOutput(
        string jsonResponse,
        string applicationId)
    {
        try
        {
            var cleanedJson = jsonResponse
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            using var doc = JsonDocument.Parse(cleanedJson);
            var root = doc.RootElement;

            var categoryStr = root.GetProperty("category").GetString();
            if (!Enum.TryParse<Category>(categoryStr, out var category))
                throw new InvalidOperationException(
                    $"Invalid category value from LLM: '{categoryStr}'. " +
                    $"Expected one of: {string.Join(", ", Enum.GetNames<Category>())}");

            var priorityStr = root.GetProperty("priority").GetString();
            if (!Enum.TryParse<Priority>(priorityStr, out var priority))
                throw new InvalidOperationException(
                    $"Invalid priority value from LLM: '{priorityStr}'. " +
                    $"Expected one of: {string.Join(", ", Enum.GetNames<Priority>())}");

            var matchScore = root.GetProperty("matchScore").GetDouble();
            if (matchScore is < 0.0 or > 1.0)
                throw new InvalidOperationException(
                    $"matchScore out of range: {matchScore}. Must be 0.0–1.0");

            var confidence = root.GetProperty("confidence").GetDouble();
            if (confidence is < 0.0 or > 1.0)
                throw new InvalidOperationException(
                    $"confidence out of range: {confidence}. Must be 0.0–1.0");

            var missingInfo = root.GetProperty("missingInfo")
                .EnumerateArray()
                .Where(item => item.ValueKind != JsonValueKind.Null)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var reasoning = root.GetProperty("reasoning").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reasoning))
                throw new InvalidOperationException("Reasoning cannot be empty");

            return new ClassificationResult
            {
                ApplicationId = applicationId,
                Category = category,
                Priority = priority,
                MatchScore = matchScore,
                Confidence = confidence,
                MissingInfo = missingInfo,
                Reasoning = reasoning,
            };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse LLM response as JSON.\nResponse: {jsonResponse}\nError: {ex.Message}", ex);
        }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"Missing required field in LLM response.\nResponse: {jsonResponse}\nError: {ex.Message}", ex);
        }
    }
}
