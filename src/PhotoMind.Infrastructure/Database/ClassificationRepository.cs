using Microsoft.EntityFrameworkCore;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;

namespace PhotoMind.Infrastructure.Database;

/// <summary>EF Core implementation of <see cref="IClassificationRepository"/> using a short-lived DbContext per call.</summary>
public sealed class ClassificationRepository : IClassificationRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ClassificationRepository(IDbContextFactory<AppDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<ClassificationTask> CreateTaskAsync(ClassificationTask task, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ClassificationTasks.Add(task);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return task;
    }

    public async Task<ClassificationTask?> GetTaskAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ClassificationTasks.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
    }

    public async Task UpdateTaskAsync(ClassificationTask task, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ClassificationTasks.Update(task);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<Image?> GetImageAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Images.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Image>> GetImagesByIdsAsync(IReadOnlyList<long> ids, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Images.Where(i => ids.Contains(i.Id)).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<long>> GetUnclassifiedImageIdsAsync(int limit, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var classified = db.ClassificationResults.Select(r => r.ImageId);
        return await db.Images
            .Where(i => i.ScanStatus == ImageScanStatus.Scanned && !classified.Contains(i.Id))
            .OrderBy(i => i.Id)
            .Take(limit)
            .Select(i => i.Id)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Category?> GetCategoryAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Categories.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
    }

    public async Task<Category?> GetCategoryByNameAsync(string name, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Categories.FirstOrDefaultAsync(c => c.Name == name, ct).ConfigureAwait(false);
    }

    public async Task AddCategoryAsync(Category category, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateCategoryAsync(Category category, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Categories.Update(category);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteCategoryAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var category = await db.Categories.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        if (category is null) return;
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Category>> GetChildCategoriesAsync(long parentId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Categories
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task SaveResultAsync(ClassificationResult result, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ClassificationResults.Add(result);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task SaveResultsAsync(IEnumerable<ClassificationResult> results, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ClassificationResults.AddRange(results);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task AddApiUsageAsync(ApiUsage usage, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ApiUsages.Add(usage);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassificationResultSummary>> GetRecentResultSummariesAsync(int take, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var rows = await (from r in db.ClassificationResults
                          join img in db.Images on r.ImageId equals img.Id
                          orderby r.Id descending
                          select new { r, img })
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

        var categories = await db.Categories.ToDictionaryAsync(c => c.Id, ct).ConfigureAwait(false);
        return rows.Select(x => ToSummary(x.r, x.img, categories)).ToList();
    }

    public async Task<IReadOnlyList<ClassificationResultSummary>> GetResultSummariesAsync(ReviewFilter filter, int skip, int take, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var search = string.IsNullOrWhiteSpace(filter.Search) ? string.Empty : filter.Search.Trim();
        var hasSearch = search.Length > 0;

        var resultQuery = db.ClassificationResults.AsQueryable();
        if (filter.CategoryId is { } categoryId)
            resultQuery = resultQuery.Where(r => r.CategoryId == categoryId);
        if (filter.SubCategoryId is { } subCategoryId)
            resultQuery = resultQuery.Where(r => r.SubCategoryId == subCategoryId);
        if (filter.MinConfidence is { } minConfidence)
            resultQuery = resultQuery.Where(r => r.Confidence >= minConfidence);
        if (filter.NeedsReviewOnly)
            resultQuery = resultQuery.Where(r => r.NeedsReview);

        var rows = await (from r in resultQuery
                          join img in db.Images on r.ImageId equals img.Id
                          where !hasSearch || img.FileName.Contains(search) || r.TagsJson.Contains(search)
                          orderby r.Id descending
                          select new { r, img })
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

        var categories = await db.Categories.ToDictionaryAsync(c => c.Id, ct).ConfigureAwait(false);
        return rows.Select(x => ToSummary(x.r, x.img, categories)).ToList();
    }

    public async Task<int> GetResultSummariesCountAsync(ReviewFilter filter, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var search = string.IsNullOrWhiteSpace(filter.Search) ? string.Empty : filter.Search.Trim();
        var hasSearch = search.Length > 0;

        var resultQuery = db.ClassificationResults.AsQueryable();
        if (filter.CategoryId is { } categoryId)
            resultQuery = resultQuery.Where(r => r.CategoryId == categoryId);
        if (filter.SubCategoryId is { } subCategoryId)
            resultQuery = resultQuery.Where(r => r.SubCategoryId == subCategoryId);
        if (filter.MinConfidence is { } minConfidence)
            resultQuery = resultQuery.Where(r => r.Confidence >= minConfidence);
        if (filter.NeedsReviewOnly)
            resultQuery = resultQuery.Where(r => r.NeedsReview);

        return await (from r in resultQuery
                      join img in db.Images on r.ImageId equals img.Id
                      where !hasSearch || img.FileName.Contains(search) || r.TagsJson.Contains(search)
                      select r.Id)
            .CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<ClassificationResult?> GetResultAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ClassificationResults.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
    }

    public async Task UpdateResultAsync(ClassificationResult result, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        result.UpdatedAt = DateTime.UtcNow;
        db.ClassificationResults.Update(result);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteResultAsync(long id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var result = await db.ClassificationResults.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        if (result is null) return;
        db.ClassificationResults.Remove(result);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassificationTask>> GetRecentClassificationTasksAsync(int take, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ClassificationTasks.AsNoTracking()
            .OrderByDescending(t => t.Id)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<ApiUsageSummary> GetUsageSummaryAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var count = await db.ApiUsages.CountAsync(ct).ConfigureAwait(false);
        if (count == 0)
            return new ApiUsageSummary();

        var totals = await db.ApiUsages
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Input = g.Sum(x => (long)x.InputTokens),
                Output = g.Sum(x => (long)x.OutputTokens),
                Total = g.Sum(x => (long)x.TotalTokens),
                Cost = g.Sum(x => (double)x.EstimatedCost),
                Estimated = g.Count(x => x.IsEstimated),
            })
            .FirstAsync(ct).ConfigureAwait(false);

        return new ApiUsageSummary
        {
            RequestCount = count,
            InputTokens = totals.Input,
            OutputTokens = totals.Output,
            TotalTokens = totals.Total,
            EstimatedCost = (decimal)totals.Cost,
            EstimatedCount = totals.Estimated,
        };
    }

    private static ClassificationResultSummary ToSummary(ClassificationResult r, Image img, IReadOnlyDictionary<long, Category> categories)
    {
        return new ClassificationResultSummary
        {
            ResultId = r.Id,
            ImageId = r.ImageId,
            FileName = img.FileName,
            OriginalPath = img.OriginalPath,
            ThumbnailPath = img.ThumbnailPath,
            CategoryName = r.CategoryId is { } categoryId && categories.TryGetValue(categoryId, out var cat) ? cat.Name : null,
            SubCategoryName = r.SubCategoryId is { } subId && categories.TryGetValue(subId, out var sub) ? sub.Name : null,
            Confidence = r.Confidence,
            NeedsReview = r.NeedsReview,
            Description = r.Description,
            Reason = r.Reason,
            TagsJson = r.TagsJson,
        };
    }
}
