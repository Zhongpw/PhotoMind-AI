using Microsoft.EntityFrameworkCore;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;

namespace PhotoMind.Infrastructure.Database;

/// <summary>
/// 整理任务的 EF Core 仓储（短生命周期 context）。
/// </summary>
public sealed class OrganizeRepository : IOrganizeRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public OrganizeRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<OrganizeTask> CreateTaskAsync(OrganizeTask task, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.OrganizeTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<OrganizeTask?> GetTaskAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.OrganizeTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task UpdateTaskAsync(OrganizeTask task, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.OrganizeTasks.Update(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizePlanItem>> GetClassifiedImagesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await (
            from result in db.ClassificationResults
            join image in db.Images on result.ImageId equals image.Id
            join category in db.Categories on result.CategoryId equals category.Id into categoryGroup
            from category in categoryGroup.DefaultIfEmpty()
            join subcategory in db.Categories on result.SubCategoryId equals subcategory.Id into subcategoryGroup
            from subcategory in subcategoryGroup.DefaultIfEmpty()
            where image.ScanStatus == ImageScanStatus.Scanned
            orderby image.Id
            select new { result, image, category, subcategory }
        ).ToListAsync(cancellationToken);

        return rows.Select(x => new OrganizePlanItem
        {
            ImageId = x.image.Id,
            FileName = x.image.FileName,
            SourcePath = x.image.OriginalPath,
            FileSize = x.image.FileSize,
            FileHash = x.image.FileHash,
            CategoryId = x.result.CategoryId,
            SubCategoryId = x.result.SubCategoryId,
            CategoryName = x.category?.Name,
            SubCategoryName = x.subcategory?.Name,
        }).ToList();
    }

    public async Task AddLogAsync(OrganizeLog log, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.OrganizeLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddLogsAsync(IEnumerable<OrganizeLog> logs, CancellationToken cancellationToken = default)
    {
        var list = logs.ToList();
        if (list.Count == 0)
            return;

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.OrganizeLogs.AddRange(list);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizeLog>> GetLogsByTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.OrganizeLogs
            .Where(l => l.TaskId == taskId)
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizeTask>> GetRecentOrganizeTasksAsync(int take, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.OrganizeTasks
            .OrderByDescending(t => t.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
