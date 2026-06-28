using System.Text.Json;
using Microsoft.Extensions.Logging;
using zionet_workflow.Models;

namespace zionet_workflow.Services;

/// <summary>
/// Appends one JSON line per triage decision to a JSON Lines (.jsonl) file.
/// Thread-safe: multiple workflow instances can write concurrently.
/// IO failures are logged as warnings and never propagate — the audit log
/// must not crash the triage workflow.
/// </summary>
public sealed class AuditLogger
{
    private readonly string _filePath;
    private readonly ILogger _logger;
    private readonly object _lock = new();

    public AuditLogger(string filePath, ILogger logger)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Serialises <paramref name="entry"/> as a single JSON line and appends it to the audit file.
    /// </summary>
    public void LogDecision(AuditEntry entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry);
            lock (_lock)
            {
                File.AppendAllText(_filePath, json + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to write audit log entry for {ApplicationId} to {FilePath}",
                entry.ApplicationId, _filePath);
        }
    }
}
