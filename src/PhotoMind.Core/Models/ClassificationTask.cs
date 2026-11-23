using PhotoMind.Core.Enums;

namespace PhotoMind.Core.Models;

/// <summary>Record of one AI classification run.</summary>
public sealed class ClassificationTask
{
    public long Id { get; set; }

    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }

    public TaskState Status { get; set; } = TaskState.Pending;

    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
