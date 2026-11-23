using PhotoMind.Core.DTOs;
using PhotoMind.Core.Models;

namespace PhotoMind.Core.Interfaces;

/// <summary>Persistence for AI classification: tasks, results, categories and API usage.</summary>
public interface IClassificationRepository
{
    Task<ClassificationTask> CreateTaskAsync(ClassificationTask task, CancellationToken ct = default);
    Task<ClassificationTask?> GetTaskAsync(long id, CancellationToken ct = default);
    Task UpdateTaskAsync(ClassificationTask task, CancellationToken ct = default);

    Task<Image?> GetImageAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Image>> GetImagesByIdsAsync(IReadOnlyList<long> ids, CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetUnclassifiedImageIdsAsync(int limit, CancellationToken ct = default);

    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct = default);
    Task<Category?> GetCategoryAsync(long id, CancellationToken ct = default);
    Task<Category?> GetCategoryByNameAsync(string name, CancellationToken ct = default);
    Task AddCategoryAsync(Category category, CancellationToken ct = default);
    Task UpdateCategoryAsync(Category category, CancellationToken ct = default);
    Task DeleteCategoryAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetChildCategoriesAsync(long parentId, CancellationToken ct = default);

    Task SaveResultAsync(ClassificationResult result, CancellationToken ct = default);
    Task SaveResultsAsync(IEnumerable<ClassificationResult> results, CancellationToken ct = default);

    Task AddApiUsageAsync(ApiUsage usage, CancellationToken ct = default);

    /// <summary>Latest classification results joined with image file name and category names.</summary>
    Task<IReadOnlyList<ClassificationResultSummary>> GetRecentResultSummariesAsync(int take, CancellationToken ct = default);

    /// <summary>Filtered, paginated classification results for the review page.</summary>
    Task<IReadOnlyList<ClassificationResultSummary>> GetResultSummariesAsync(ReviewFilter filter, int skip, int take, CancellationToken ct = default);
    Task<int> GetResultSummariesCountAsync(ReviewFilter filter, CancellationToken ct = default);

    Task<ClassificationResult?> GetResultAsync(long id, CancellationToken ct = default);
    Task UpdateResultAsync(ClassificationResult result, CancellationToken ct = default);
    Task DeleteResultAsync(long id, CancellationToken ct = default);

    /// <summary>Recent classification tasks (history page).</summary>
    Task<IReadOnlyList<ClassificationTask>> GetRecentClassificationTasksAsync(int take, CancellationToken ct = default);

    /// <summary>Aggregated API usage (request count, tokens, estimated cost).</summary>
    Task<ApiUsageSummary> GetUsageSummaryAsync(CancellationToken ct = default);
}
