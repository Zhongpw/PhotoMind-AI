using PhotoMind.Core.Enums;

namespace PhotoMind.Core.DTOs;

/// <summary>Progress snapshot emitted by <see cref="Interfaces.IClassificationService"/> during a run.</summary>
public sealed class AiClassifyProgress
{
    public long TaskId { get; set; }
    public TaskState Status { get; set; }
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public string? CurrentFile { get; set; }
    public string? ErrorMessage { get; set; }
}
