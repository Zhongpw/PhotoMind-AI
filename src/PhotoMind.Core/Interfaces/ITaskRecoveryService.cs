using PhotoMind.Core.DTOs;

namespace PhotoMind.Core.Interfaces;

/// <summary>
/// 任务恢复：应用启动时把上次因软件异常退出而停留在 Running/Pending 的任务标记为 Failed，
/// 保证数据库任务状态可恢复，避免"僵尸"运行态任务。
/// </summary>
public interface ITaskRecoveryService
{
    Task<TaskRecoveryResult> RecoverInterruptedTasksAsync(CancellationToken cancellationToken = default);
}
