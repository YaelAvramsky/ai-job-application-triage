using Microsoft.Extensions.Logging;
using zionet_workflow.Models;

namespace zionet_workflow.Executors;

/// <summary>
/// Router executor: decides which path the application takes.
/// Implements business rules as deterministic code over the classifier's output.
/// Low-confidence classifications (< 0.65) are always escalated to human review,
/// because an uncertain model call should not trigger an automated accept or reject.
/// </summary>
public class RouterExecutor
{
    private const double ConfidenceThreshold = 0.65;

    private readonly ILogger _logger;

    public RouterExecutor(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public RoutingDecision Route(ClassificationResult classification)
    {
        _logger.LogInformation(
            "Routing application {Id} (category={Category}, confidence={Confidence:P0})",
            classification.ApplicationId, classification.Category, classification.Confidence);

        var route = DetermineRoute(classification);
        var reason = GenerateRouteReason(classification, route);

        var decision = new RoutingDecision
        {
            ApplicationId = classification.ApplicationId,
            Route = route,
            Reason = reason,
        };

        _logger.LogInformation(
            "Routed {Id} → {Route}: {Reason}",
            classification.ApplicationId, route, reason);

        return decision;
    }

    private static RoutePath DetermineRoute(ClassificationResult c)
    {
        // Model wasn't confident → always send to a human regardless of category
        if (c.Confidence < ConfidenceThreshold)
            return RoutePath.PendingHumanReview;

        return c.Category switch
        {
            Category.StrongCandidate when c.Priority == Priority.High => RoutePath.AutoApprove,
            Category.FitCandidate => RoutePath.PendingHumanReview,
            Category.Incomplete => RoutePath.RequestMoreInfo,
            _ => RoutePath.Rejected,   // WeakCandidate or StrongCandidate with non-High priority
        };
    }

    private static string GenerateRouteReason(ClassificationResult c, RoutePath route)
    {
        var confidenceNote = c.Confidence < ConfidenceThreshold
            ? $" (low model confidence: {c.Confidence:P0})"
            : string.Empty;

        return route switch
        {
            RoutePath.AutoApprove =>
                $"Strong match (score: {c.MatchScore:P0}, confidence: {c.Confidence:P0}). " +
                "Automatically approved for interview.",

            RoutePath.PendingHumanReview =>
                $"Requires human judgment{confidenceNote}. " +
                $"Score: {c.MatchScore:P0}. " +
                (c.MissingInfo.Count > 0
                    ? $"Flagged gaps: {string.Join(", ", c.MissingInfo.Take(2))}"
                    : "Borderline classification."),

            RoutePath.RequestMoreInfo =>
                "Application incomplete. Please provide: " +
                string.Join(", ", c.MissingInfo),

            RoutePath.Rejected =>
                $"Does not meet minimum qualifications " +
                $"(score: {c.MatchScore:P0}, confidence: {c.Confidence:P0}).",

            _ => "Unknown routing path.",
        };
    }
}
