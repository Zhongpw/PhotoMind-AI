using PhotoMind.Core.Enums;

namespace PhotoMind.Core.Models;

/// <summary>Record of one scan run. SourcePath holds a JSON array when multiple roots are scanned.</summary>
public sealed class ScanTask
{
    public long Id { get; set; }

    /// <summary>JSON-encoded string array of scanned root paths (single path is serialised as a 1-element array).</summary>
    public string SourcePath { get; set; } = "[]";

    public int TotalCount { get; set; }
    public int ScannedCount { get; set; }
    public int FailedCount { get; set; }

    public TaskState Status { get; set; } = TaskState.Pending;

    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
