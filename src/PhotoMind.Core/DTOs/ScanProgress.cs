using PhotoMind.Core.Enums;

namespace PhotoMind.Core.DTOs;

/// <summary>Snapshot of scan progress, pushed to the UI via IProgress&lt;ScanProgress&gt;.</summary>
public sealed class ScanProgress
{
    public long ScanTaskId { get; set; }

    public TaskState Status { get; set; } = TaskState.Running;

    /// <summary>Total image files discovered (may grow while directory enumeration is in progress).</summary>
    public int TotalCount { get; set; }

    public int ScannedCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>Most recent file being processed (for a live status line).</summary>
    public string? CurrentFile { get; set; }

    /// <summary>Set when the task finishes or fails.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Accumulated per-file errors (bounded by the scan service).</summary>
    public List<ScanError> Errors { get; } = new();
}
