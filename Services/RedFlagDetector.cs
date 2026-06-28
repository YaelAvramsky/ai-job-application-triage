using System.Text.RegularExpressions;
using zionet_workflow.Models;

namespace zionet_workflow.Services;

/// <summary>
/// Deterministic keyword and regex checks applied before the LLM classifier.
/// Each rule targets a structural quality problem that can be detected without AI.
/// Returns a list of flag-name constants — callers compare against the constants on this class.
/// </summary>
public static class RedFlagDetector
{
    /// <summary>Cover letter is shorter than 20 characters — effectively empty.</summary>
    public const string CoverLetterTooShort = "CoverLetterTooShort";

    /// <summary>Cover letter contains generic filler phrases indicating no real effort.</summary>
    public const string GenericCoverLetter = "GenericCoverLetter";

    /// <summary>Zero years of experience AND no skills listed — application provides nothing to evaluate.</summary>
    public const string ZeroExperienceNoSkills = "ZeroExperienceNoSkills";

    /// <summary>Claims 5+ years but lists zero skills — internally inconsistent.</summary>
    public const string ExperienceSkillMismatch = "ExperienceSkillMismatch";

    /// <summary>Lists 5+ skills but claims zero years — internally inconsistent.</summary>
    public const string SkillsWithoutExperience = "SkillsWithoutExperience";

    private static readonly Regex GenericPhrases = new(
        @"i want to work|hire me|looking for a job",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Runs all deterministic rules against the raw application.
    /// Returns the flag-name constants for every rule that fired.
    /// An empty list means no issues were detected.
    /// </summary>
    public static IReadOnlyList<string> Detect(JobApplication app)
    {
        var flags = new List<string>();

        if (app.CoverLetter.Length < 20)
            flags.Add(CoverLetterTooShort);

        if (GenericPhrases.IsMatch(app.CoverLetter))
            flags.Add(GenericCoverLetter);

        if (app.YearsOfExperience == 0 && app.Skills.Count == 0)
            flags.Add(ZeroExperienceNoSkills);

        if (app.YearsOfExperience >= 5 && app.Skills.Count == 0)
            flags.Add(ExperienceSkillMismatch);

        if (app.Skills.Count >= 5 && app.YearsOfExperience == 0)
            flags.Add(SkillsWithoutExperience);

        return flags;
    }
}
