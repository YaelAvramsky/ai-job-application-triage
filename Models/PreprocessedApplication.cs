using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zionet_workflow.Models;

/// <summary>
/// Preprocessed application with PII masked and extracted metadata.
/// </summary>
public sealed record PreprocessedApplication
{
    public string Id { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public int YearsOfExperience { get; init; }
    public List<string> Skills { get; init; } = [];
    public string CoverLetterSummary { get; init; } = string.Empty;
    public int CoverLetterLength { get; init; }
    public bool HasRequiredSkills { get; init; }
    public string MaskedEmail { get; init; } = string.Empty;
    
    /// <summary>Raw data for classifier—text analysis needs substance.</summary>
    public string FullApplicationText { get; init; } = string.Empty;

    /// <summary>Flag identifiers from <see cref="Services.RedFlagDetector"/> — empty means no issues detected.</summary>
    public IReadOnlyList<string> RedFlags { get; init; } = [];
}
