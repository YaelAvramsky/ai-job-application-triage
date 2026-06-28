namespace zionet_workflow.Models;

/// <summary>
/// Immutable record of a single triage decision written to the JSON Lines audit log.
/// Enums are captured as strings so the file is human-readable without a converter.
/// </summary>
public sealed record AuditEntry
{
    public required string ApplicationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>String representation of <see cref="Category"/>.</summary>
    public required string Category { get; init; }

    /// <summary>String representation of <see cref="Priority"/>.</summary>
    public required string Priority { get; init; }

    public required double MatchScore { get; init; }
    public required double Confidence { get; init; }

    /// <summary>String representation of <see cref="RoutePath"/>.</summary>
    public required string Route { get; init; }

    public required IReadOnlyList<string> RedFlags { get; init; }
    public required string Reasoning { get; init; }

    /// <summary>The <see cref="ApprovalResult.Status"/> value after the approval step.</summary>
    public required string FinalStatus { get; init; }
}
