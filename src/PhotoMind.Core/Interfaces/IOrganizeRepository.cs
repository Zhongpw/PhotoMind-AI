using PhotoMind.Core.DTOs;
using PhotoMind.Core.Models;

namespace PhotoMind.Core.Interfaces;

/// <summary>
/// 整理任务的持久化仓储。
/// </summary>
public interface IOrganizeRepository
{
    Task<OrganizeTask> CreateTaskAsync(OrganizeTask task, CancellationToken cancellationToken = default);
    Task<OrganizeTask?> GetTaskAsync(long id, CancellationToken cancellationToken = default);
    Task UpdateTaskAsync(OrganizeTask task, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizePlanItem>> GetClassifiedImagesAsync(CancellationToken cancellationToken = default);
    Task AddLogAsync(OrganizeLog log, CancellationToken cancellationToken = default);
    Task AddLogsAsync(IEnumerable<OrganizeLog> logs, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizeLog>> GetLogsByTaskAsync(long taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizeTask>> GetRecentOrganizeTasksAsync(int take, CancellationToken cancellationToken = default);
}
