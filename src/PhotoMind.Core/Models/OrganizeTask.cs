using PhotoMind.Core.Enums;

namespace PhotoMind.Core.Models;

/// <summary>Record of one organise run (create-folders / copy / move).</summary>
public sealed class OrganizeTask
{
    public long Id { get; set; }

    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;

    public OrganizeOperationType OperationType { get; set; } = OrganizeOperationType.None;

    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }

    public TaskState Status { get; set; } = TaskState.Pending;

    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
