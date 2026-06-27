using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zionet_workflow.Models;

/// <summary>
/// Represents a raw job application submission.
/// </summary>
public sealed record JobApplication
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string ApplicantName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string CoverLetter { get; init; } = string.Empty;
    public int YearsOfExperience { get; init; }
    public List<string> Skills { get; init; } = [];
    public DateTime SubmittedAt { get; init; } = DateTime.UtcNow;
}