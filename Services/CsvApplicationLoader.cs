using System.Text;
using zionet_workflow.Models;

namespace zionet_workflow.Services;

/// <summary>
/// Loads <see cref="JobApplication"/> objects from a CSV file.
/// The file must have a mandatory header row with columns in this exact order:
/// Id, ApplicantName, Email, Position, YearsOfExperience, Skills, CoverLetter
/// The Skills column is semicolon-separated (e.g. C#;SQL;Azure).
/// Fields may be enclosed in double quotes (RFC 4180); a literal quote inside a
/// quoted field is escaped as "".
/// </summary>
public static class CsvApplicationLoader
{
    private static readonly string[] ExpectedColumns =
        ["Id", "ApplicantName", "Email", "Position", "YearsOfExperience", "Skills", "CoverLetter"];

    /// <summary>
    /// Parses <paramref name="filePath"/> and returns all valid rows as job applications.
    /// Blank lines are skipped. Throws <see cref="InvalidOperationException"/> on any
    /// structural error, with the 1-based line number and column name in the message.
    /// </summary>
    public static IReadOnlyList<JobApplication> Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new InvalidOperationException($"CSV file not found: '{filePath}'.");

        var lines = File.ReadAllLines(filePath);

        if (lines.Length == 0)
            throw new InvalidOperationException("CSV file is empty.");

        var header = SplitRow(lines[0]).Select(h => h.Trim()).ToArray();
        ValidateHeader(header);

        var applications = new List<JobApplication>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var fields = SplitRow(lines[i]);
            applications.Add(ParseRow(fields, lineNumber: i + 1));
        }

        return applications;
    }

    private static void ValidateHeader(string[] header)
    {
        for (int i = 0; i < ExpectedColumns.Length; i++)
        {
            if (i >= header.Length)
                throw new InvalidOperationException(
                    $"CSV header is missing column '{ExpectedColumns[i]}' (expected at position {i + 1}).");

            if (header[i] != ExpectedColumns[i])
                throw new InvalidOperationException(
                    $"CSV header column {i + 1}: expected '{ExpectedColumns[i]}', found '{header[i]}'.");
        }
    }

    private static JobApplication ParseRow(string[] fields, int lineNumber)
    {
        if (fields.Length < ExpectedColumns.Length)
            throw new InvalidOperationException(
                $"Line {lineNumber}: expected {ExpectedColumns.Length} columns, found {fields.Length}.");

        var id = RequireField(fields[0], "Id", lineNumber);
        var name = RequireField(fields[1], "ApplicantName", lineNumber);
        var email = RequireField(fields[2], "Email", lineNumber);
        var position = RequireField(fields[3], "Position", lineNumber);

        if (!int.TryParse(fields[4].Trim(), out var yearsOfExperience))
            throw new InvalidOperationException(
                $"Line {lineNumber}: column 'YearsOfExperience' must be an integer, " +
                $"found '{fields[4].Trim()}'.");

        var skillsRaw = fields[5].Trim();
        var skills = string.IsNullOrWhiteSpace(skillsRaw)
            ? []
            : skillsRaw
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

        var coverLetter = fields[6].Trim();

        return new JobApplication
        {
            Id = id,
            ApplicantName = name,
            Email = email,
            Position = position,
            YearsOfExperience = yearsOfExperience,
            Skills = skills,
            CoverLetter = coverLetter,
        };
    }

    private static string RequireField(string value, string columnName, int lineNumber)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException(
                $"Line {lineNumber}: required column '{columnName}' is empty.");
        return trimmed;
    }

    /// <summary>
    /// Splits one CSV line into fields, handling RFC 4180 quoted fields.
    /// A field enclosed in double quotes may contain commas and escaped quotes ("").
    /// </summary>
    private static string[] SplitRow(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // consume the escaped second quote
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                    inQuotes = true;
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                    current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}
