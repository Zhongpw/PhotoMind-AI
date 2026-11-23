using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure.FileSystem;
using Serilog;

namespace PhotoMind.Infrastructure.Services;

/// <summary>
/// 照片整理服务：预览、执行（建目录/复制/移动）、撤销。所有文件系统操作均由 C# 完成，AI 不参与。
/// </summary>
public sealed class OrganizeService : IOrganizeService
{
    private const int LogFlushBatchSize = 50;

    private readonly IOrganizeRepository _repository;
    private readonly IFileHashService _hashService;
    private readonly SafeFileOperations _fileOperations;
    private readonly ILogger _logger;

    public OrganizeService(
        IOrganizeRepository repository,
        IFileHashService hashService,
        SafeFileOperations fileOperations,
        ILogger logger)
    {
        _repository = repository;
        _hashService = hashService;
        _fileOperations = fileOperations;
        _logger = logger;
    }

    public async Task<OrganizePreview> PreviewAsync(OrganizeOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        var planItems = await _repository.GetClassifiedImagesAsync(cancellationToken);
        var filtered = FilterByIds(planItems, options.ImageIds);
        var items = BuildPreviewItems(filtered, options.DestinationPath);

        return new OrganizePreview { Items = items };
    }

    public async Task<OrganizeTask> ExecuteAsync(
        OrganizeOptions options,
        PauseToken pauseToken,
        IProgress<OrganizeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        var planItems = await _repository.GetClassifiedImagesAsync(cancellationToken);
        var filtered = FilterByIds(planItems, options.ImageIds);
        var items = BuildPreviewItems(filtered, options.DestinationPath);

        // 磁盘空间检查（复制 / 跨盘移动前）。
        if (options.OperationType is OrganizeOperationType.Copy or OrganizeOperationType.Move)
        {
            var requiredBytes = items.Where(i => i.ErrorMessage is null).Sum(i => i.FileSize);
            if (requiredBytes > 0 && !SafeFileOperations.HasEnoughSpace(options.DestinationPath, requiredBytes))
                throw new IOException("目标磁盘空间不足，无法完成整理。");
        }

        var sourceDirectories = string.Join(";",
            filtered.Select(i => Path.GetDirectoryName(i.SourcePath))
                .Where(d => d is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase));

        var task = new OrganizeTask
        {
            SourcePath = sourceDirectories,
            DestinationPath = options.DestinationPath,
            OperationType = options.OperationType,
            TotalCount = items.Count,
            CompletedCount = 0,
            FailedCount = 0,
            Status = TaskState.Running,
            StartedAt = DateTime.UtcNow,
        };
        await _repository.CreateTaskAsync(task, cancellationToken);

        var completed = 0;
        var failed = 0;
        var pendingLogs = new List<OrganizeLog>();

        try
        {
            // 创建所有分类目录（含"仅建目录"模式）。
            foreach (var directory in items
                         .Select(i => i.DestinationDirectory)
                         .Where(d => !string.IsNullOrEmpty(d))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
                Directory.CreateDirectory(directory);

            if (options.OperationType == OrganizeOperationType.CreateFoldersOnly)
            {
                completed = items.Count;
            }
            else
            {
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await WaitWhilePausedAsync(pauseToken, cancellationToken);

                    progress?.Report(new OrganizeProgress
                    {
                        TaskId = task.Id,
                        Status = TaskState.Running,
                        TotalCount = items.Count,
                        CompletedCount = completed,
                        FailedCount = failed,
                        CurrentFile = item.FileName,
                    });

                    try
                    {
                        if (item.ErrorMessage is not null)
                            throw new InvalidOperationException(item.ErrorMessage);

                        if (!File.Exists(item.SourcePath))
                            throw new FileNotFoundException("源文件不存在或已被移动。", item.SourcePath);

                        var destinationPath = item.DestinationPath;
                        if (File.Exists(destinationPath))
                            destinationPath = FileConflictResolver.GetUniquePath(destinationPath);

                        var log = new OrganizeLog
                        {
                            TaskId = task.Id,
                            ImageId = item.ImageId,
                            SourcePath = item.SourcePath,
                            DestinationPath = destinationPath,
                            OperationType = options.OperationType,
                            FileHashBefore = item.FileHash,
                            Status = TaskState.Running,
                            CreatedAt = DateTime.UtcNow,
                        };

                        switch (options.OperationType)
                        {
                            case OrganizeOperationType.Copy:
                                await _fileOperations.CopyWithVerificationAsync(item.SourcePath, destinationPath, cancellationToken);
                                break;
                            case OrganizeOperationType.Move:
                                await _fileOperations.MoveAsync(item.SourcePath, destinationPath, cancellationToken);
                                break;
                        }

                        log.FileHashAfter = await _hashService.ComputeSha256Async(destinationPath, cancellationToken);
                        log.Status = TaskState.Completed;
                        pendingLogs.Add(log);
                        completed++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        pendingLogs.Add(new OrganizeLog
                        {
                            TaskId = task.Id,
                            ImageId = item.ImageId,
                            SourcePath = item.SourcePath,
                            DestinationPath = item.DestinationPath,
                            OperationType = options.OperationType,
                            FileHashBefore = item.FileHash,
                            Status = TaskState.Failed,
                            ErrorMessage = ex.Message,
                            CreatedAt = DateTime.UtcNow,
                        });
                        _logger.Warning(ex, "整理文件失败: {Source}", item.SourcePath);
                    }

                    if (pendingLogs.Count >= LogFlushBatchSize)
                    {
                        await _repository.AddLogsAsync(pendingLogs, cancellationToken);
                        pendingLogs.Clear();
                    }
                }
            }

            task.CompletedCount = completed;
            task.FailedCount = failed;
            task.Status = failed > 0 && completed == 0 ? TaskState.Failed : TaskState.Completed;
            task.FinishedAt = DateTime.UtcNow;

            if (pendingLogs.Count > 0)
                await _repository.AddLogsAsync(pendingLogs, cancellationToken);
            await _repository.UpdateTaskAsync(task, cancellationToken);
            return task;
        }
        catch (OperationCanceledException)
        {
            task.CompletedCount = completed;
            task.FailedCount = failed;
            task.Status = TaskState.Cancelled;
            task.FinishedAt = DateTime.UtcNow;
            await TryFlushLogsAsync(pendingLogs);
            await TryFinalizeAsync(task);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "整理任务异常终止: TaskId={TaskId}", task.Id);
            task.CompletedCount = completed;
            task.FailedCount = failed;
            task.Status = TaskState.Failed;
            task.FinishedAt = DateTime.UtcNow;
            await TryFlushLogsAsync(pendingLogs);
            await TryFinalizeAsync(task);
            throw;
        }
    }

    public async Task<OrganizeUndoResult> UndoAsync(
        long taskId,
        PauseToken pauseToken,
        IProgress<OrganizeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var task = await _repository.GetTaskAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException("找不到要撤销的整理任务。");

        var logs = await _repository.GetLogsByTaskAsync(taskId, cancellationToken);
        var completedLogs = logs
            .Where(l => l.Status == TaskState.Completed &&
                        l.OperationType is OrganizeOperationType.Copy or OrganizeOperationType.Move)
            .ToList();

        var reverted = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var log in completedLogs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WaitWhilePausedAsync(pauseToken, cancellationToken);

            progress?.Report(new OrganizeProgress
            {
                TaskId = taskId,
                Status = TaskState.Running,
                TotalCount = completedLogs.Count,
                CompletedCount = reverted,
                FailedCount = failed,
                CurrentFile = Path.GetFileName(log.DestinationPath),
            });

            try
            {
                if (log.OperationType == OrganizeOperationType.Copy)
                {
                    // 复制撤销：删除本次创建的副本；删除前核实文件身份（哈希一致，防误删用户改动过的文件）。
                    if (File.Exists(log.DestinationPath))
                    {
                        if (!string.IsNullOrEmpty(log.FileHashAfter))
                        {
                            var currentHash = await _hashService.ComputeSha256Async(log.DestinationPath, cancellationToken);
                            if (!string.Equals(currentHash, log.FileHashAfter, StringComparison.OrdinalIgnoreCase))
                                throw new IOException($"目标文件身份校验失败，拒绝删除：{log.DestinationPath}");
                        }
                        File.Delete(log.DestinationPath);
                    }
                    reverted++;
                }
                else if (log.OperationType == OrganizeOperationType.Move)
                {
                    // 移动撤销：把目标文件恢复回原始位置；原位置已存在同名文件则拒绝覆盖。
                    if (File.Exists(log.DestinationPath))
                    {
                        if (File.Exists(log.SourcePath))
                            throw new IOException($"原始位置已存在文件，不能覆盖：{log.SourcePath}");

                        await _fileOperations.MoveAsync(log.DestinationPath, log.SourcePath, cancellationToken);
                    }
                    reverted++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{Path.GetFileName(log.DestinationPath)}: {ex.Message}");
                _logger.Warning(ex, "撤销整理失败: {Destination}", log.DestinationPath);
            }
        }

        return new OrganizeUndoResult { RevertedCount = reverted, FailedCount = failed, Errors = errors };
    }

    private async Task TryFlushLogsAsync(List<OrganizeLog> logs)
    {
        try
        {
            if (logs.Count > 0)
                await _repository.AddLogsAsync(logs, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "写入整理日志失败");
        }
    }

    private async Task TryFinalizeAsync(OrganizeTask task)
    {
        try
        {
            await _repository.UpdateTaskAsync(task, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "更新整理任务状态失败: TaskId={TaskId}", task.Id);
        }
    }

    private static IReadOnlyList<OrganizePlanItem> FilterByIds(IReadOnlyList<OrganizePlanItem> items, IReadOnlyList<long> imageIds)
    {
        if (imageIds.Count == 0)
            return items;

        var set = imageIds.ToHashSet();
        return items.Where(i => set.Contains(i.ImageId)).ToList();
    }

    private static IReadOnlyList<OrganizePreviewItem> BuildPreviewItems(IReadOnlyList<OrganizePlanItem> planItems, string destinationPath)
    {
        var result = new List<OrganizePreviewItem>(planItems.Count);
        var destinationRoot = Path.GetFullPath(destinationPath);

        foreach (var item in planItems)
        {
            var categoryName = PathSafety.SanitizeFolderName(item.CategoryName ?? "其他");
            var subcategoryName = string.IsNullOrWhiteSpace(item.SubCategoryName)
                ? null
                : PathSafety.SanitizeFolderName(item.SubCategoryName);

            var directory = subcategoryName is null
                ? Path.Combine(destinationRoot, categoryName)
                : Path.Combine(destinationRoot, categoryName, subcategoryName);

            var destinationFile = Path.Combine(directory, Path.GetFileName(item.SourcePath));

            string? error = null;
            if (!File.Exists(item.SourcePath))
                error = "源文件不存在";
            else if (Path.GetFullPath(item.SourcePath).Equals(destinationFile, StringComparison.OrdinalIgnoreCase))
                error = "源文件与目标位置相同";

            result.Add(new OrganizePreviewItem
            {
                ImageId = item.ImageId,
                FileName = item.FileName,
                SourcePath = item.SourcePath,
                FileHash = item.FileHash,
                CategoryName = categoryName,
                SubCategoryName = subcategoryName,
                DestinationDirectory = directory,
                DestinationPath = destinationFile,
                FileSize = item.FileSize,
                DestinationExists = File.Exists(destinationFile),
                ErrorMessage = error,
            });
        }

        return result;
    }

    private static void ValidateOptions(OrganizeOptions options)
    {
        PathSafety.ValidateDestination(options.DestinationPath);

        if (options.OperationType == OrganizeOperationType.None)
            throw new ArgumentException("请选择整理操作类型（复制/移动/仅创建目录）。");
    }

    private static async Task WaitWhilePausedAsync(PauseToken pauseToken, CancellationToken cancellationToken)
    {
        while (pauseToken.IsPaused)
        {
            var pauseTask = pauseToken.WaitWhilePausedAsync();
            var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completed = await Task.WhenAny(pauseTask, cancelTask);
            if (completed == cancelTask)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
