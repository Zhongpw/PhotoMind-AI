using PhotoMind.Core.Enums;

namespace PhotoMind.Core.Models;

/// <summary>Per-file audit entry written by the organiser; the source of truth for the undo feature.</summary>
public sealed class OrganizeLog
{
    public long Id { get; set; }

    public long TaskId { get; set; }
    public long ImageId { get; set; }

    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;

    public OrganizeOperationType OperationType { get; set; } = OrganizeOperationType.None;

    public string FileHashBefore { get; set; } = string.Empty;
    public string FileHashAfter { get; set; } = string.Empty;

    public TaskState Status { get; set; } = TaskState.Pending;

    public string ErrorMessage { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
