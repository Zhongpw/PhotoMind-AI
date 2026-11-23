using System.Text.Json;
using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure.AI;
using Serilog;

namespace PhotoMind.Infrastructure.Services;

/// <summary>
/// Orchestrates an AI classification run: previews each image, sends a vision request with retry,
/// parses + validates the result, resolves the category, and persists the result and API usage.
/// </summary>
public sealed class ClassificationService : IClassificationService
{
    private readonly IClassificationRepository _repo;
    private readonly IImageNormalizer _normalizer;
    private readonly AIRequestManager _requestManager;
    private readonly IAiConfigStore _configStore;
    private readonly ISecretStore _secretStore;
    private readonly ILogger _logger;

    public ClassificationService(
        IClassificationRepository repo,
        IImageNormalizer normalizer,
        AIRequestManager requestManager,
        IAiConfigStore configStore,
        ISecretStore secretStore,
        ILogger logger)
    {
        _repo = repo;
        _normalizer = normalizer;
        _requestManager = requestManager;
        _configStore = configStore;
        _secretStore = secretStore;
        _logger = logger;
    }

    public async Task<ClassificationTask> ClassifyAsync(
        AiClassifyOptions options,
        PauseToken pauseToken,
        IProgress<AiClassifyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var config = (await _configStore.LoadAsync(cancellationToken).ConfigureAwait(false)).Sanitized();

        if (!config.AllowUpload)
            throw new PhotoMindConfigurationException("隐私设置禁止 AI 图片上传，请先在设置中开启。");

        var apiKey = await _secretStore.GetSecretAsync(AiConstants.ApiKeySecretName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new PhotoMindConfigurationException("尚未配置 DeepSeek API Key，请先在设置中填写。");

        var categories = await _repo.GetCategoriesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<long> imageIds = options.ImageIds.Count > 0
            ? options.ImageIds
            : await _repo.GetUnclassifiedImageIdsAsync(int.MaxValue, cancellationToken).ConfigureAwait(false);

        if (imageIds.Count == 0)
        {
            return await _repo.CreateTaskAsync(new ClassificationTask
            {
                TotalCount = 0,
                Status = TaskState.Completed,
                StartedAt = DateTime.UtcNow,
                FinishedAt = DateTime.UtcNow,
            }, cancellationToken).ConfigureAwait(false);
        }

        var images = await _repo.GetImagesByIdsAsync(imageIds, cancellationToken).ConfigureAwait(false);

        var task = new ClassificationTask
        {
            TotalCount = images.Count,
            PendingCount = images.Count,
            Status = TaskState.Running,
            StartedAt = DateTime.UtcNow,
        };
        task = await _repo.CreateTaskAsync(task, cancellationToken).ConfigureAwait(false);

        var completed = 0;
        var failed = 0;

        Report(progress, task.Id, TaskState.Running, task.TotalCount, 0, 0, null, null);

        try
        {
            using var semaphore = new SemaphoreSlim(config.Concurrency);

            var workItems = images.Select(async image =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await WaitWhilePausedAsync(pauseToken, cancellationToken).ConfigureAwait(false);

                    var ok = await ProcessImageAsync(image, task.Id, config, apiKey, options.Mode, categories, cancellationToken).ConfigureAwait(false);

                    if (ok) Interlocked.Increment(ref completed);
                    else Interlocked.Increment(ref failed);

                    task.CompletedCount = completed;
                    task.FailedCount = failed;
                    task.PendingCount = task.TotalCount - completed - failed;

                    Report(progress, task.Id, TaskState.Running, task.TotalCount, completed, failed, image.FileName, null);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(workItems).ConfigureAwait(false);

            task.Status = TaskState.Completed;
        }
        catch (OperationCanceledException)
        {
            task.Status = TaskState.Cancelled;
            await FinalizeTaskAsync(task, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (DeepSeekException ex) when (ex.Kind is DeepSeekErrorKind.Authentication or DeepSeekErrorKind.InsufficientBalance)
        {
            task.Status = TaskState.Failed;
            _logger.Error(ex, "分类任务 {TaskId} 中止：{Kind} {Message}", task.Id, ex.Kind, ex.Message);
            await FinalizeTaskAsync(task, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            task.Status = TaskState.Failed;
            _logger.Error(ex, "分类任务 {TaskId} 失败", task.Id);
            await FinalizeTaskAsync(task, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await FinalizeTaskAsync(task, cancellationToken).ConfigureAwait(false);
        Report(progress, task.Id, task.Status, task.TotalCount, task.CompletedCount, task.FailedCount, null, null);
        return task;
    }

    private async Task<bool> ProcessImageAsync(
        Image image,
        long taskId,
        AiConfig config,
        string apiKey,
        ClassificationMode mode,
        IReadOnlyList<Category> categories,
        CancellationToken ct)
    {
        AiPreview? preview = null;
        try
        {
            preview = await _normalizer.CreatePreviewAsync(image.OriginalPath, config, ct).ConfigureAwait(false);

            var bytes = await File.ReadAllBytesAsync(preview.TempFilePath, ct).ConfigureAwait(false);
            var dataUrl = $"data:{preview.MimeType};base64,{Convert.ToBase64String(bytes)}";

            var request = new DeepSeekRequest
            {
                Model = config.Model,
                Messages = PromptBuilder.BuildMessages(dataUrl, mode, categories, config.Detail),
            };

            var response = await _requestManager.SendWithRetryAsync(config.BaseUrl, apiKey, request, config.MaxRetries, ct).ConfigureAwait(false);

            var content = response.Choices.FirstOrDefault()?.Message.Content;
            if (string.IsNullOrWhiteSpace(content))
                throw new DeepSeekException(DeepSeekErrorKind.InvalidResponse, "AI 返回了空内容。");

            var parsed = AIResponseParser.Parse(content);
            var (categoryId, subCategoryId, needsReview) = await ResolveCategoriesAsync(parsed, mode, categories, ct).ConfigureAwait(false);

            var result = new ClassificationResult
            {
                ImageId = image.Id,
                CategoryId = categoryId,
                SubCategoryId = subCategoryId,
                Description = parsed.Description,
                Reason = parsed.Reason,
                Confidence = parsed.Confidence,
                TagsJson = JsonSerializer.Serialize(parsed.Tags),
                NeedsReview = needsReview,
                ModelName = config.Model,
                PromptVersion = PromptBuilder.PromptVersion,
            };

            await _repo.SaveResultAsync(result, ct).ConfigureAwait(false);
            await RecordUsageAsync(taskId, response.Usage, config.Model, dataUrl.Length, ct).ConfigureAwait(false);
            return true;
        }
        catch (DeepSeekException ex) when (ex.Kind is DeepSeekErrorKind.Authentication or DeepSeekErrorKind.InsufficientBalance)
        {
            throw; // Fatal: abort the entire task.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warning(ex, "图片 {ImageId} ({FileName}) 分类失败", image.Id, image.FileName);
            return false;
        }
        finally
        {
            if (preview != null)
                _normalizer.DeletePreview(preview.TempFilePath);
        }
    }

    private async Task<(long? categoryId, long? subCategoryId, bool needsReview)> ResolveCategoriesAsync(
        AiRecognitionResult parsed,
        ClassificationMode mode,
        IReadOnlyList<Category> categories,
        CancellationToken ct)
    {
        var top = categories.FirstOrDefault(c => c.ParentId == null && c.Name == parsed.Category);
        long? categoryId = top?.Id;
        long? subCategoryId = null;
        var needsReview = parsed.NeedsReview;

        if (top == null && !parsed.IsUnclassified)
        {
            if (mode == ClassificationMode.Fixed)
                return (null, null, true); // Reject: leave unclassified and flag for review.

            var newCategory = new Category
            {
                Name = parsed.Category,
                ParentId = null,
                Description = parsed.Description,
                IsSystem = false,
            };
            await _repo.AddCategoryAsync(newCategory, ct).ConfigureAwait(false);
            categoryId = newCategory.Id;
            needsReview = true;
        }

        if (categoryId != null && !string.IsNullOrWhiteSpace(parsed.Subcategory))
        {
            var child = categories.FirstOrDefault(c => c.ParentId == categoryId && c.Name == parsed.Subcategory);
            if (child != null)
            {
                subCategoryId = child.Id;
            }
            else if (mode != ClassificationMode.Fixed)
            {
                var newChild = new Category
                {
                    Name = parsed.Subcategory,
                    ParentId = categoryId,
                    IsSystem = false,
                };
                await _repo.AddCategoryAsync(newChild, ct).ConfigureAwait(false);
                subCategoryId = newChild.Id;
                needsReview = true;
            }
        }

        return (categoryId, subCategoryId, needsReview);
    }

    private async Task RecordUsageAsync(long taskId, DeepSeekUsage? usage, string modelName, int dataUrlLength, CancellationToken ct)
    {
        int input, output, total;
        bool estimated;

        if (usage != null)
        {
            input = usage.PromptTokens;
            output = usage.CompletionTokens;
            total = usage.TotalTokens > 0 ? usage.TotalTokens : input + output;
            estimated = false;
        }
        else
        {
            input = Math.Max(1, dataUrlLength / 4);
            output = 100;
            total = input + output;
            estimated = true;
        }

        await _repo.AddApiUsageAsync(new ApiUsage
        {
            TaskId = taskId,
            ModelName = modelName,
            InputTokens = input,
            OutputTokens = output,
            TotalTokens = total,
            EstimatedCost = 0,
            IsEstimated = estimated,
            RequestStatus = "Success",
        }, ct).ConfigureAwait(false);
    }

    private async Task FinalizeTaskAsync(ClassificationTask task, CancellationToken ct)
    {
        task.FinishedAt = DateTime.UtcNow;
        await _repo.UpdateTaskAsync(task, ct).ConfigureAwait(false);
    }

    private static async Task WaitWhilePausedAsync(PauseToken pauseToken, CancellationToken ct)
    {
        while (pauseToken.IsPaused)
        {
            var pauseTask = pauseToken.WaitWhilePausedAsync();
            var cancelTask = Task.Delay(Timeout.Infinite, ct);
            var completed = await Task.WhenAny(pauseTask, cancelTask).ConfigureAwait(false);
            if (completed == cancelTask)
                ct.ThrowIfCancellationRequested();
        }
    }

    private static void Report(
        IProgress<AiClassifyProgress>? progress,
        long taskId,
        TaskState status,
        int total,
        int completed,
        int failed,
        string? currentFile,
        string? error)
    {
        progress?.Report(new AiClassifyProgress
        {
            TaskId = taskId,
            Status = status,
            TotalCount = total,
            CompletedCount = completed,
            FailedCount = failed,
            CurrentFile = currentFile,
            ErrorMessage = error,
        });
    }
}
