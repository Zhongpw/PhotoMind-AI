using PhotoMind.Core.Enums;

namespace PhotoMind.Core.DTOs;

/// <summary>
/// 整理任务进度。
/// </summary>
public sealed class OrganizeProgress
{
    public long TaskId { get; init; }
    public TaskState Status { get; init; }
    public int TotalCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public string? CurrentFile { get; init; }
    public string? ErrorMessage { get; init; }
}
