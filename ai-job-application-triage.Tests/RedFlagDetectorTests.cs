using Xunit;
using zionet_workflow.Models;
using zionet_workflow.Services;

namespace zionet_workflow.Tests;

public sealed class RedFlagDetectorTests
{
    // ─── Helper ───────────────────────────────────────────────────────────────

    private static JobApplication Make(
        string coverLetter = "I am a passionate developer with strong C# and Azure experience, eager to join your team.",
        int yearsOfExperience = 3,
        List<string>? skills = null) =>
        new()
        {
            Id = "TEST",
            ApplicantName = "Test User",
            Email = "test@example.com",
            Position = "Developer",
            CoverLetter = coverLetter,
            YearsOfExperience = yearsOfExperience,
            Skills = skills ?? ["C#", "SQL", "Azure"],
        };

    // ─── CoverLetterTooShort ─────────────────────────────────────────────────

    [Fact]
    public void CoverLetterTooShort_Fires_WhenLetterHasFewerThan20Chars()
    {
        var app = Make(coverLetter: "Short.");
        Assert.Contains(RedFlagDetector.CoverLetterTooShort, RedFlagDetector.Detect(app));
    }

    [Fact]
    public void CoverLetterTooShort_Silent_WhenLetterHas20OrMoreChars()
    {
        var app = Make(coverLetter: "This letter is long enough to pass.");
        Assert.DoesNotContain(RedFlagDetector.CoverLetterTooShort, RedFlagDetector.Detect(app));
    }

    // ─── GenericCoverLetter ───────────────────────────────────────────────────

    [Theory]
    [InlineData("I want to work at your company.")]
    [InlineData("Please hire me for this position.")]
    [InlineData("I am looking for a job in software development.")]
    public void GenericCoverLetter_Fires_WhenPhraseDetected(string letter)
    {
        var app = Make(coverLetter: letter);
        Assert.Contains(RedFlagDetector.GenericCoverLetter, RedFlagDetector.Detect(app));
    }

    [Fact]
    public void GenericCoverLetter_Silent_WhenLetterIsSpecific()
    {
        var app = Make(coverLetter: "I built a distributed microservices platform using C# and Azure Service Bus.");
        Assert.DoesNotContain(RedFlagDetector.GenericCoverLetter, RedFlagDetector.Detect(app));
    }

    [Fact]
    public void GenericCoverLetter_Fires_CaseInsensitive()
    {
        var app = Make(coverLetter: "HIRE ME — I am the best candidate.");
        Assert.Contains(RedFlagDetector.GenericCoverLetter, RedFlagDetector.Detect(app));
    }

    // ─── ZeroExperienceNoSkills ───────────────────────────────────────────────

    [Fact]
    public void ZeroExperienceNoSkills_Fires_WhenBothAreZero()
    {
        var app = Make(yearsOfExperience: 0, skills: []);
        Assert.Contains(RedFlagDetector.ZeroExperienceNoSkills, RedFlagDetector.Detect(app));
    }

    [Fact]
    public void ZeroExperienceNoSkills_Silent_WhenSkillsPresent()
    {
        var app = Make(yearsOfExperience: 0, skills: ["C#"]);
        Assert.DoesNotContain(RedFlagDetector.ZeroExperienceNoSkills, RedFlagDetector.Detect(app));
    }

    // ─── ExperienceSkillMismatch ─────────────────────────────────────────────

    [Fact]
    public void ExperienceSkillMismatch_Fires_WhenHighExperienceButNoSkills()
    {
        var app = Make(yearsOfExperience: 5, skills: []);
        Assert.Contains(RedFlagDetector.ExperienceSkillMismatch, RedFlagDetector.Detect(app));
    }

    [Fact]
    public void ExperienceSkillMismatch_Silent_WhenSkillsMatchExperience()
    {
        var app = Make(yearsOfExperience: 5, skills: ["C#", "SQL"]);
        Assert.DoesNotContain(RedFlagDetector.ExperienceSkillMismatch, RedFlagDetector.Detect(app));
    }

    // ─── SkillsWithoutExperience ─────────────────────────────────────────────

    [Fact]
    public void SkillsWithoutExperience_Fires_WhenManySkillsButZeroYears()
    {
        var app = Make(yearsOfExperience: 0, skills: ["C#", "SQL", "Azure", "Docker", "Git"]);
        Assert.Contains(RedFlagDetector.SkillsWithoutExperience, RedFlagDetector.Detect(app));
    }

    [Fact]
    public void SkillsWithoutExperience_Silent_WhenExperienceIsPresent()
    {
        var app = Make(yearsOfExperience: 1, skills: ["C#", "SQL", "Azure", "Docker", "Git"]);
        Assert.DoesNotContain(RedFlagDetector.SkillsWithoutExperience, RedFlagDetector.Detect(app));
    }

    // ─── Clean application produces no flags ─────────────────────────────────

    [Fact]
    public void Detect_ReturnsEmpty_ForCleanApplication()
    {
        var app = Make();
        Assert.Empty(RedFlagDetector.Detect(app));
    }
}
