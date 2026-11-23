namespace PhotoMind.Core.DTOs;

/// <summary>任务恢复结果：软件启动时把因异常退出而中断（Running/Pending）的任务标记为 Failed 的数量。</summary>
public sealed class TaskRecoveryResult
{
    public int RecoveredScanTasks { get; init; }
    public int RecoveredClassificationTasks { get; init; }
    public int RecoveredOrganizeTasks { get; init; }
    public int TotalRecovered => RecoveredScanTasks + RecoveredClassificationTasks + RecoveredOrganizeTasks;
}
