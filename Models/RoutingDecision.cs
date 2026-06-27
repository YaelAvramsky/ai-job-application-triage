namespace zionet_workflow.Models;

/// <summary>
/// Represents the routing decision: which path the application takes.
/// </summary>
public sealed record RoutingDecision
{
    public string ApplicationId { get; init; } = string.Empty;
    public RoutePath Route { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime DecidedAt { get; init; } = DateTime.UtcNow;
}

public enum RoutePath
{
    AutoApprove,              // Straight to interview
    PendingHumanReview,       // Needs approval before next step
    Rejected,                 // Does not fit
    RequestMoreInfo,          // Ask applicant for details
}