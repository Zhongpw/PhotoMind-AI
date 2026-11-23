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

    Task SaveResultAsync(ClassificationResult result, CancellationToken ct = default);
    Task SaveResultsAsync(IEnumerable<ClassificationResult> results, CancellationToken ct = default);

    Task AddApiUsageAsync(ApiUsage usage, CancellationToken ct = default);

    /// <summary>Latest classification results joined with image file name and category names.</summary>
    Task<IReadOnlyList<ClassificationResultSummary>> GetRecentResultSummariesAsync(int take, CancellationToken ct = default);
}
