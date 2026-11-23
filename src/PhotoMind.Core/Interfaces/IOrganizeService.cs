using PhotoMind.Core.DTOs;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;

namespace PhotoMind.Core.Interfaces;

/// <summary>
/// 照片整理服务：预览、执行（建目录/复制/移动）、撤销。
/// </summary>
public interface IOrganizeService
{
    Task<OrganizePreview> PreviewAsync(OrganizeOptions options, CancellationToken cancellationToken = default);
    Task<OrganizeTask> ExecuteAsync(OrganizeOptions options, PauseToken pauseToken, IProgress<OrganizeProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<OrganizeUndoResult> UndoAsync(long taskId, PauseToken pauseToken, IProgress<OrganizeProgress>? progress = null, CancellationToken cancellationToken = default);
}
