using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zionet_workflow.Models;

namespace zionet_workflow.SampleData;

/// <summary>
/// Sample job applications for testing the workflow.
/// Demonstrates different paths: auto-approve, human review, incomplete, rejected.
/// </summary>
public static class SampleApplications
{
    /// <summary>Strong candidate — should auto-approve.</summary>
    public static JobApplication StrongCandidate => new()
    {
        Id = "APP-001",
        ApplicantName = "Alice Johnson",
        Email = "alice.johnson@email.com",
        PhoneNumber = "+1-555-0101",
        Position = "Senior C# Developer",
        YearsOfExperience = 8,
        Skills = ["C#", "SQL", "Azure", "Microservices", ".NET 8"],
        CoverLetter = """
            I am a passionate software engineer with 8 years of experience in building enterprise applications 
            using C# and Azure. I have extensive expertise in SQL, microservices architecture, and modern .NET development. 
            I am excited about this opportunity and confident I can deliver immediate value to your team.
            """,
    };

    /// <summary>Fit candidate — should go to human review.</summary>
    public static JobApplication FitCandidate => new()
    {
        Id = "APP-002",
        ApplicantName = "Bob Smith",
        Email = "bob.smith@email.com",
        PhoneNumber = "+1-555-0102",
        Position = "C# Developer",
        YearsOfExperience = 4,
        Skills = ["C#", "SQL", "JavaScript"],
        CoverLetter = """
            I have 4 years of experience as a developer working with C# and SQL. 
            While I haven't used Azure before, I am a quick learner and excited to expand my skills.
            """,
    };

    /// <summary>Incomplete application — should request more info.</summary>
    public static JobApplication IncompleteApplication => new()
    {
        Id = "APP-003",
        ApplicantName = "Carol White",
        Email = "carol.white@email.com",
        PhoneNumber = "+1-555-0103",
        Position = "Developer",
        YearsOfExperience = 0,
        Skills = [],
        CoverLetter = "I want to work.",
    };

    /// <summary>Weak candidate — requires human approval before rejection.</summary>
    public static JobApplication WeakCandidate => new()
    {
        Id = "APP-004",
        ApplicantName = "David Brown",
        Email = "david.brown@email.com",
        PhoneNumber = "+1-555-0104",
        Position = "Developer",
        YearsOfExperience = 1,
        Skills = ["HTML", "CSS", "JavaScript"],
        CoverLetter = "I know web development.",
    };

    public static List<JobApplication> GetAllSamples() =>
    [
        StrongCandidate,
        FitCandidate,
        IncompleteApplication,
        WeakCandidate,
    ];
}
