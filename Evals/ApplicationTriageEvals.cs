using Microsoft.Extensions.Logging;
using zionet_workflow.Executors;
using zionet_workflow.Models;
using zionet_workflow.Services;

namespace zionet_workflow.Evals;

/// <summary>
/// Labeled evaluation cases for the classifier.
/// Each case pairs a concrete input with the expected category and priority so that
/// regressions surface immediately — a sample run without expected-output checks
/// can never catch a broken classification.
/// </summary>
public sealed record EvalCase
{
    public required string Description { get; init; }
    public required JobApplication Input { get; init; }
    public required Category ExpectedCategory { get; init; }
    public required Priority ExpectedPriority { get; init; }
}

public sealed record EvalResult
{
    public required EvalCase Case { get; init; }
    public required ClassificationResult Actual { get; init; }
    public bool CategoryMatch => Actual.Category == Case.ExpectedCategory;
    public bool PriorityMatch => Actual.Priority == Case.ExpectedPriority;
    public bool Passed => CategoryMatch && PriorityMatch;
}

/// <summary>
/// Runs the classifier against all labeled eval cases and reports pass / fail.
/// </summary>
public class EvalRunner
{
    private readonly ClassifierExecutor _classifier;
    private readonly ILogger _logger;

    public EvalRunner(ClassifierExecutor classifier, ILogger logger)
    {
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static IReadOnlyList<EvalCase> Cases { get; } =
    [
        new EvalCase
        {
            Description = "Clear StrongCandidate — 8 yrs exp, all required skills, detailed cover letter",
            Input = new JobApplication
            {
                Id = "EVAL-001",
                ApplicantName = "Alice Johnson",
                Email = "alice@example.com",
                Position = "Senior C# Developer",
                YearsOfExperience = 8,
                Skills = ["C#", "SQL", "Azure", "Microservices", ".NET 8"],
                CoverLetter = """
                    I am a software engineer with 8 years building enterprise C# applications on Azure.
                    I have deep expertise in SQL, microservices architecture, and modern .NET — I am confident
                    I can deliver immediate value to your team and help modernize your data pipeline.
                    """,
            },
            ExpectedCategory = Category.StrongCandidate,
            ExpectedPriority = Priority.High,
        },

        new EvalCase
        {
            Description = "FitCandidate — 4 yrs exp, C# + SQL but missing Azure",
            Input = new JobApplication
            {
                Id = "EVAL-002",
                ApplicantName = "Bob Smith",
                Email = "bob@example.com",
                Position = "C# Developer",
                YearsOfExperience = 4,
                Skills = ["C#", "SQL", "JavaScript", "React"],
                CoverLetter = """
                    I have 4 years of experience as a C# and SQL developer. I have not used Azure
                    yet but I am a quick learner and actively studying for AZ-900. I am excited
                    about this opportunity to grow into cloud development.
                    """,
            },
            ExpectedCategory = Category.FitCandidate,
            ExpectedPriority = Priority.Medium,
        },

        new EvalCase
        {
            Description = "Incomplete — no skills listed, near-empty cover letter, zero experience",
            Input = new JobApplication
            {
                Id = "EVAL-003",
                ApplicantName = "Carol White",
                Email = "carol@example.com",
                Position = "Developer",
                YearsOfExperience = 0,
                Skills = [],
                CoverLetter = "I want to work.",
            },
            ExpectedCategory = Category.Incomplete,
            ExpectedPriority = Priority.Low,
        },

        new EvalCase
        {
            Description = "WeakCandidate — wrong domain (front-end only), 1 yr exp, no required skills",
            Input = new JobApplication
            {
                Id = "EVAL-004",
                ApplicantName = "David Brown",
                Email = "david@example.com",
                Position = "Developer",
                YearsOfExperience = 1,
                Skills = ["HTML", "CSS", "JavaScript", "Figma"],
                CoverLetter = "I know web design and front-end development.",
            },
            ExpectedCategory = Category.WeakCandidate,
            ExpectedPriority = Priority.Low,
        },

        new EvalCase
        {
            Description = "Edge case — high experience (10 yrs) but entirely wrong tech stack (Python / Java)",
            Input = new JobApplication
            {
                Id = "EVAL-005",
                ApplicantName = "Eve Martinez",
                Email = "eve@example.com",
                Position = "Senior C# Developer",
                YearsOfExperience = 10,
                Skills = ["Python", "Java", "Django", "Spring Boot", "AWS"],
                CoverLetter = """
                    I am a senior engineer with 10 years of experience in Python and Java microservices.
                    I have never worked with C# or Azure but I am willing to learn. My background in
                    distributed systems should transfer well.
                    """,
            },
            ExpectedCategory = Category.WeakCandidate,
            ExpectedPriority = Priority.Low,
        },

        new EvalCase
        {
            Description = "Borderline FitCandidate — 3 yrs exp, has all skills but thin cover letter",
            Input = new JobApplication
            {
                Id = "EVAL-006",
                ApplicantName = "Frank Lee",
                Email = "frank@example.com",
                Position = "C# Developer",
                YearsOfExperience = 3,
                Skills = ["C#", "SQL", "Azure"],
                CoverLetter = "Experienced C# and Azure developer. Available immediately.",
            },
            ExpectedCategory = Category.FitCandidate,
            ExpectedPriority = Priority.Medium,
        },
    ];

    public async Task<IReadOnlyList<EvalResult>> RunAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<EvalResult>();

        foreach (var evalCase in Cases)
        {
            _logger.LogInformation("  Running eval: {Description}", evalCase.Description);

            var preprocessed = PreprocessExecutor.Preprocess(evalCase.Input);
            var actual = await _classifier.ClassifyAsync(preprocessed, cancellationToken);

            results.Add(new EvalResult { Case = evalCase, Actual = actual });
        }

        return results;
    }

    public static void PrintReport(IReadOnlyList<EvalResult> results)
    {
        var passed = results.Count(r => r.Passed);
        var total = results.Count;

        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine($"  │  EVAL RESULTS: {passed}/{total} passed                                    │");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────┘");

        foreach (var r in results)
        {
            var status = r.Passed ? "PASS" : "FAIL";
            var icon = r.Passed ? "✓" : "✗";
            Console.WriteLine();
            Console.WriteLine($"  {icon} [{status}] {r.Case.Description}");
            Console.WriteLine($"        Category : expected={r.Case.ExpectedCategory,-18} actual={r.Actual.Category}");
            Console.WriteLine($"        Priority : expected={r.Case.ExpectedPriority,-18} actual={r.Actual.Priority}");
            Console.WriteLine($"        Score={r.Actual.MatchScore:P0}  Confidence={r.Actual.Confidence:P0}");

            if (!r.Passed)
            {
                Console.WriteLine($"        Reasoning: {r.Actual.Reasoning}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  Summary: {passed}/{total} passed ({(double)passed / total:P0})");
    }
}
