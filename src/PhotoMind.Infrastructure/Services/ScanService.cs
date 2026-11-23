using System.Text.Json;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure.FileSystem;
using Serilog;

namespace PhotoMind.Infrastructure.Services;

/// <summary>
/// Orchestrates a scan task: creates the <see cref="ScanTask"/> row, enumerates candidate files asynchronously,
/// processes each file into a Scanned/Failed/Skipped outcome, and commits in batches. Original files are only read.
/// </summary>
public sealed class ScanService : IScanService
{
    private const int HeaderBytes = 16;
    private const int BatchSize = 100;
    private const int ReportInterval = 20;
    private const int MaxTrackedErrors = 500;

    private readonly DirectoryScanner _scanner;
    private readonly IImageFormatDetector _detector;
    private readonly IFileHashService _hash;
    private readonly IImageMetadataReader _metadata;
    private readonly IThumbnailService _thumbnails;
    private readonly IScanRepository _repository;

    public ScanService(
        DirectoryScanner scanner,
        IImageFormatDetector detector,
        IFileHashService hash,
        IImageMetadataReader metadata,
        IThumbnailService thumbnails,
        IScanRepository repository)
    {
        _scanner = scanner;
        _detector = detector;
        _hash = hash;
        _metadata = metadata;
        _thumbnails = thumbnails;
        _repository = repository;
    }

    public async Task<ScanTask> ScanAsync(
        ScanOptions options,
        PauseToken pauseToken,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var task = new ScanTask
        {
            SourcePath = JsonSerializer.Serialize(options.SourcePaths),
            Status = TaskState.Running,
            StartedAt = DateTime.UtcNow,
        };
        task = await _repository.CreateTaskAsync(task, cancellationToken).ConfigureAwait(false);

        var report = new ScanProgress { ScanTaskId = task.Id, Status = TaskState.Running };
        var batch = new List<Image>(BatchSize);
        int total = 0, scanned = 0, failed = 0;

        try
        {
            await foreach (var file in _scanner.EnumerateCandidateFilesAsync(
                               options.SourcePaths,
                               options.ExcludedDirectories,
                               options.Recursive,
                               cancellationToken).ConfigureAwait(false))
            {
                await pauseToken.WaitWhilePausedAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                total++;
                report.TotalCount = total;
                report.CurrentFile = file;

                var outcome = await ProcessFileAsync(file, task.Id, options, cancellationToken).ConfigureAwait(false);
                switch (outcome.Result)
                {
                    case ProcessResult.Scanned:
                        scanned++;
                        batch.Add(outcome.Image!);
                        break;
                    case ProcessResult.Failed:
                        failed++;
                        batch.Add(outcome.Image!);
                        if (report.Errors.Count < MaxTrackedErrors)
                            report.Errors.Add(new ScanError(file, outcome.ErrorMessage ?? "读取失败"));
                        break;
                    case ProcessResult.Skipped:
                        break;
                }

                if (batch.Count >= BatchSize)
                    await FlushAsync(batch, cancellationToken).ConfigureAwait(false);

                report.ScannedCount = scanned;
                report.FailedCount = failed;
                if (total % ReportInterval == 0)
                    progress?.Report(report);
            }

            if (batch.Count > 0)
                await FlushAsync(batch, cancellationToken).ConfigureAwait(false);

            task.TotalCount = total;
            task.ScannedCount = scanned;
            task.FailedCount = failed;
            task.Status = TaskState.Completed;
            task.FinishedAt = DateTime.UtcNow;
            await _repository.UpdateTaskAsync(task, cancellationToken).ConfigureAwait(false);

            report.Status = TaskState.Completed;
            report.CurrentFile = null;
            progress?.Report(report);
            return task;
        }
        catch (OperationCanceledException)
        {
            await TryFinalizeAsync(task, TaskState.Cancelled, total, scanned, failed).ConfigureAwait(false);
            report.Status = TaskState.Cancelled;
            progress?.Report(report);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "扫描任务失败 (task {TaskId})", task.Id);
            await TryFinalizeAsync(task, TaskState.Failed, total, scanned, failed).ConfigureAwait(false);
            report.Status = TaskState.Failed;
            report.ErrorMessage = ex.Message;
            progress?.Report(report);
            throw;
        }
    }

    private async Task<(ProcessResult Result, Image? Image, string? ErrorMessage)> ProcessFileAsync(
        string file, long taskId, ScanOptions options, CancellationToken cancellationToken)
    {
        ImageFormat format;
        try
        {
            await using var stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                HeaderBytes, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[HeaderBytes];
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            format = _detector.Detect(buffer.AsSpan(0, read));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            const string message = "无法读取文件（文件可能被占用或无权限）";
            return (ProcessResult.Failed, MakeFailedRow(file, taskId), message);
        }

        if (format == ImageFormat.Unknown || !_detector.IsSupported(format))
            return (ProcessResult.Skipped, null, null);

        if (options.AllowedFormats.Count > 0 && !options.AllowedFormats.Contains(format))
            return (ProcessResult.Skipped, null, null);

        var fileInfo = new FileInfo(file);
        try
        {
            var hash = await _hash.ComputeSha256Async(file, cancellationToken).ConfigureAwait(false);
            var metadata = await _metadata.ReadAsync(file, format, cancellationToken).ConfigureAwait(false);
            var thumbnail = await _thumbnails.GetOrCreateThumbnailAsync(file, cancellationToken).ConfigureAwait(false);

            return (ProcessResult.Scanned, new Image
            {
                FileName = Path.GetFileName(file),
                OriginalPath = Path.GetFullPath(file),
                FileSize = fileInfo.Length,
                FileHash = hash,
                Width = metadata.Width,
                Height = metadata.Height,
                Format = format,
                CreatedAt = fileInfo.CreationTimeUtc,
                ModifiedAt = fileInfo.LastWriteTimeUtc,
                ExifDate = metadata.ExifDate,
                ScanStatus = ImageScanStatus.Scanned,
                ScanTaskId = taskId,
                CreatedTime = DateTime.UtcNow,
                ThumbnailPath = thumbnail,
            }, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "图片处理失败: {Path}", file);
            return (ProcessResult.Failed, MakeFailedRow(file, taskId), ex.Message);
        }
    }

    private static Image MakeFailedRow(string file, long taskId)
    {
        var fileInfo = new FileInfo(file);
        return new Image
        {
            FileName = Path.GetFileName(file),
            OriginalPath = Path.GetFullPath(file),
            FileSize = fileInfo.Exists ? fileInfo.Length : 0,
            FileHash = string.Empty,
            Format = ImageFormat.Unknown,
            ScanStatus = ImageScanStatus.Failed,
            ScanTaskId = taskId,
            CreatedTime = DateTime.UtcNow,
        };
    }

    private async Task FlushAsync(List<Image> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;
        await _repository.AddImagesAsync(batch, cancellationToken).ConfigureAwait(false);
        batch.Clear();
    }

    private async Task TryFinalizeAsync(ScanTask task, TaskState status, int total, int scanned, int failed)
    {
        try
        {
            task.TotalCount = total;
            task.ScannedCount = scanned;
            task.FailedCount = failed;
            task.Status = status;
            task.FinishedAt = DateTime.UtcNow;
            await _repository.UpdateTaskAsync(task, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "扫描任务状态写入失败 (task {TaskId})", task.Id);
        }
    }

    private enum ProcessResult
    {
        Scanned,
        Failed,
        Skipped,
    }
}
