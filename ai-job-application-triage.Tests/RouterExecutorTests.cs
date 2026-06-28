using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using zionet_workflow.Executors;
using zionet_workflow.Models;
using zionet_workflow.Services;

namespace zionet_workflow.Tests;

public sealed class RouterExecutorTests
{
    private readonly RouterExecutor _router = new(NullLogger.Instance);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ClassificationResult MakeResult(
        Category category = Category.StrongCandidate,
        Priority priority = Priority.High,
        double confidence = 0.9,
        double matchScore = 0.85) =>
        new()
        {
            ApplicationId = "TEST",
            Category = category,
            Priority = priority,
            Confidence = confidence,
            MatchScore = matchScore,
        };

    private static PreprocessedApplication MakePreprocessed(
        IReadOnlyList<string>? redFlags = null) =>
        new() { RedFlags = redFlags ?? Array.Empty<string>() };

    // ─── LLM-based routing paths ──────────────────────────────────────────────

    [Fact]
    public void Route_AutoApprove_ForStrongCandidateHighPriorityHighConfidence()
    {
        var decision = _router.Route(
            MakeResult(Category.StrongCandidate, Priority.High, confidence: 0.9));

        Assert.Equal(RoutePath.AutoApprove, decision.Route);
    }

    [Fact]
    public void Route_AutoApprove_WhenConfidenceIsExactlyAtThreshold()
    {
        // 0.65 is not < 0.65, so it should pass through to the category switch
        var decision = _router.Route(
            MakeResult(Category.StrongCandidate, Priority.High, confidence: 0.65));

        Assert.Equal(RoutePath.AutoApprove, decision.Route);
    }

    [Fact]
    public void Route_PendingHumanReview_WhenConfidenceBelowThreshold()
    {
        // Even a StrongCandidate + High is escalated when the model is uncertain
        var decision = _router.Route(
            MakeResult(Category.StrongCandidate, Priority.High, confidence: 0.64));

        Assert.Equal(RoutePath.PendingHumanReview, decision.Route);
    }

    [Fact]
    public void Route_PendingHumanReview_ForFitCandidate()
    {
        var decision = _router.Route(
            MakeResult(Category.FitCandidate, Priority.Medium, confidence: 0.8));

        Assert.Equal(RoutePath.PendingHumanReview, decision.Route);
    }

    [Fact]
    public void Route_RequestMoreInfo_ForIncomplete()
    {
        var decision = _router.Route(
            MakeResult(Category.Incomplete, Priority.Low, confidence: 0.8));

        Assert.Equal(RoutePath.RequestMoreInfo, decision.Route);
    }

    [Fact]
    public void Route_Rejected_ForWeakCandidate()
    {
        var decision = _router.Route(
            MakeResult(Category.WeakCandidate, Priority.Low, confidence: 0.8));

        Assert.Equal(RoutePath.Rejected, decision.Route);
    }

    [Fact]
    public void Route_Rejected_ForStrongCandidateWithNonHighPriority()
    {
        // StrongCandidate + Medium/Low falls to the _ (default) case → Rejected
        var decision = _router.Route(
            MakeResult(Category.StrongCandidate, Priority.Medium, confidence: 0.9));

        Assert.Equal(RoutePath.Rejected, decision.Route);
    }

    // ─── Red-flag overrides ───────────────────────────────────────────────────

    [Fact]
    public void Route_RequestMoreInfo_WhenZeroExperienceNoSkillsFlag_OverridesLlm()
    {
        // LLM classified as StrongCandidate — red flag wins
        var classification = MakeResult(Category.StrongCandidate, Priority.High, confidence: 0.95);
        var preprocessed = MakePreprocessed([RedFlagDetector.ZeroExperienceNoSkills]);

        var decision = _router.Route(classification, preprocessed);

        Assert.Equal(RoutePath.RequestMoreInfo, decision.Route);
    }

    [Fact]
    public void Route_FollowsLlm_WhenOnlyGenericCoverLetterFlag()
    {
        // GenericCoverLetter logs a warning but does NOT change routing
        var classification = MakeResult(Category.StrongCandidate, Priority.High, confidence: 0.9);
        var preprocessed = MakePreprocessed([RedFlagDetector.GenericCoverLetter]);

        var decision = _router.Route(classification, preprocessed);

        Assert.Equal(RoutePath.AutoApprove, decision.Route);
    }

    [Fact]
    public void Route_NoFlagsOverload_BehavesIdenticallyToSingleArgOverload()
    {
        var classification = MakeResult(Category.FitCandidate, Priority.Medium, confidence: 0.8);
        var withoutPreprocessed = _router.Route(classification);
        var withEmptyFlags = _router.Route(classification, MakePreprocessed());

        Assert.Equal(withoutPreprocessed.Route, withEmptyFlags.Route);
    }
}
