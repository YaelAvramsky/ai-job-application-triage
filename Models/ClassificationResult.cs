using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zionet_workflow.Models;

/// <summary>
/// Structured classification output from the agent node.
/// Session 1 skill: structured output inside a workflow node.
/// </summary>
public sealed record ClassificationResult
{
    /// <summary>Set by application code after parsing — not produced by the LLM.</summary>
    [SchemaIgnore]
    public string ApplicationId { get; init; } = string.Empty;

    public Category Category { get; init; }
    public Priority Priority { get; init; }

    /// <summary>How well the candidate fits the role (0 = no fit, 1 = perfect fit).</summary>
    public double MatchScore { get; init; }

    /// <summary>
    /// How certain the model is about this classification (0 = very uncertain, 1 = very certain).
    /// Low confidence routes the application to human review regardless of category.
    /// </summary>
    public double Confidence { get; init; }

    public List<string> MissingInfo { get; init; } = [];
    public string Reasoning { get; init; } = string.Empty;
}

public enum Category
{
    StrongCandidate,      
    FitCandidate,         
    WeakCandidate,        
    Incomplete,           
}

public enum Priority
{
    High,
    Medium,
    Low,
}
