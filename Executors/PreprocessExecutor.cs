using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using zionet_workflow.Models;

namespace zionet_workflow.Executors;

/// <summary>
/// Plain-function executor node: masks PII, extracts metadata.
/// This is deterministic—no LLM calls, no loops, predictable output.
/// </summary>
public static class PreprocessExecutor
{
    private static readonly string[] RequiredSkills = ["C#", "SQL", "Azure"];

    public static PreprocessedApplication Preprocess(
        JobApplication app,
        ILogger? logger = null)
    {
        logger?.LogInformation("Preprocessing application {Id} from {Applicant}",
            app.Id, app.ApplicantName);

        var maskedEmail = MaskEmail(app.Email);
        var hasRequiredSkills = HasAllRequiredSkills(app.Skills);
        var coverLetterSummary = SummarizeCoverLetter(app.CoverLetter);

        var preprocessed = new PreprocessedApplication
        {
            Id = app.Id,
            Position = app.Position,
            YearsOfExperience = app.YearsOfExperience,
            Skills = app.Skills,
            CoverLetterSummary = coverLetterSummary,
            CoverLetterLength = app.CoverLetter.Length,
            HasRequiredSkills = hasRequiredSkills,
            MaskedEmail = maskedEmail,
            FullApplicationText = BuildFullText(app),
        };

        logger?.LogInformation(
            "Preprocessed: required skills match={HasRequired}, email={MaskedEmail}",
            hasRequiredSkills, maskedEmail);

        return preprocessed;
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "***@***";
        var localPart = parts[0];
        var masked = localPart.Length > 2
            ? localPart[0] + new string('*', localPart.Length - 2) + localPart[^1]
            : "***";
        return $"{masked}@{parts[1]}";
    }

    private static bool HasAllRequiredSkills(List<string> skills)
        => RequiredSkills.All(req => skills.Any(s => s.Contains(req, StringComparison.OrdinalIgnoreCase)));

    private static string SummarizeCoverLetter(string coverLetter)
        => coverLetter.Length > 200
            ? coverLetter[..200] + "..."
            : coverLetter;

    private static string BuildFullText(JobApplication app)
        => $"""
            Position: {app.Position}
            Experience: {app.YearsOfExperience} years
            Skills: {string.Join(", ", app.Skills)}
            Cover Letter: {app.CoverLetter}
            """;
}
