using Xunit;
using zionet_workflow.Executors;
using zionet_workflow.Models;
using zionet_workflow.Services;

namespace zionet_workflow.Tests;

public sealed class PreprocessExecutorTests
{
    // ─── Helper ───────────────────────────────────────────────────────────────

    private static JobApplication Make(
        string email = "test@example.com",
        List<string>? skills = null,
        string coverLetter = "I have extensive C# and Azure experience and am excited to join your team.",
        int yearsOfExperience = 3) =>
        new()
        {
            Id = "TEST-001",
            ApplicantName = "Test User",
            Email = email,
            Position = "C# Developer",
            YearsOfExperience = yearsOfExperience,
            Skills = skills ?? ["C#", "SQL", "Azure"],
            CoverLetter = coverLetter,
        };

    // ─── Email masking ────────────────────────────────────────────────────────

    [Fact]
    public void MaskEmail_MasksLocalPart_LeavingFirstAndLastChar()
    {
        var preprocessed = PreprocessExecutor.Preprocess(Make(email: "alice.johnson@example.com"));

        // "alice.johnson" is 13 chars → first + 11 stars + last
        Assert.Equal("a***********n@example.com", preprocessed.MaskedEmail);
    }

    [Fact]
    public void MaskEmail_HandlesShortLocalPart()
    {
        var preprocessed = PreprocessExecutor.Preprocess(Make(email: "ab@example.com"));

        // Length ≤ 2 → falls back to "***"
        Assert.Equal("***@example.com", preprocessed.MaskedEmail);
    }

    [Fact]
    public void MaskEmail_ReturnsFallback_ForInvalidFormat()
    {
        var preprocessed = PreprocessExecutor.Preprocess(Make(email: "not-an-email"));
        Assert.Equal("***@***", preprocessed.MaskedEmail);
    }

    // ─── Required skills detection ────────────────────────────────────────────

    [Fact]
    public void HasRequiredSkills_True_WhenAllThreePresent()
    {
        var preprocessed = PreprocessExecutor.Preprocess(Make(skills: ["C#", "SQL", "Azure"]));
        Assert.True(preprocessed.HasRequiredSkills);
    }

    [Fact]
    public void HasRequiredSkills_True_CaseInsensitive()
    {
        var preprocessed = PreprocessExecutor.Preprocess(Make(skills: ["c#", "sql", "azure"]));
        Assert.True(preprocessed.HasRequiredSkills);
    }

    [Fact]
    public void HasRequiredSkills_False_WhenAzureMissing()
    {
        var preprocessed = PreprocessExecutor.Preprocess(Make(skills: ["C#", "SQL"]));
        Assert.False(preprocessed.HasRequiredSkills);
    }

    [Fact]
    public void HasRequiredSkills_False_WhenNoSkills()
    {
        var preprocessed = PreprocessExecutor.Preprocess(Make(skills: []));
        Assert.False(preprocessed.HasRequiredSkills);
    }

    // ─── Cover letter length ──────────────────────────────────────────────────

    [Fact]
    public void CoverLetterLength_MatchesRawLength()
    {
        const string letter = "Exactly fifty characters long cover letter here!!";
        var preprocessed = PreprocessExecutor.Preprocess(Make(coverLetter: letter));
        Assert.Equal(letter.Length, preprocessed.CoverLetterLength);
    }

    // ─── Red flags integration ────────────────────────────────────────────────

    [Fact]
    public void RedFlags_PopulatedFromDetector_WhenFlagsPresent()
    {
        // 0 experience + 0 skills → ZeroExperienceNoSkills
        var app = Make(skills: [], yearsOfExperience: 0,
            coverLetter: "This is a valid cover letter with enough text to pass the length check.");

        var preprocessed = PreprocessExecutor.Preprocess(app);

        Assert.Contains(RedFlagDetector.ZeroExperienceNoSkills, preprocessed.RedFlags);
    }

    [Fact]
    public void RedFlags_Empty_ForCleanApplication()
    {
        var preprocessed = PreprocessExecutor.Preprocess(Make());
        Assert.Empty(preprocessed.RedFlags);
    }
}
