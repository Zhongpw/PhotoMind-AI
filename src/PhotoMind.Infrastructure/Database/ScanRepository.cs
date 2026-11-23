using Microsoft.EntityFrameworkCore;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;

namespace PhotoMind.Infrastructure.Database;

/// <summary>
/// EF Core implementation of <see cref="IScanRepository"/>. Uses a context factory so every call gets its
/// own short-lived context (safe for concurrent/background use and never shared across threads).
/// </summary>
public sealed class ScanRepository : IScanRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ScanRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<ScanTask> CreateTaskAsync(ScanTask task, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.ScanTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task<ScanTask?> GetTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.ScanTasks.FindAsync(new object[] { taskId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateTaskAsync(ScanTask task, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.ScanTasks.Update(task);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddImageAsync(Image image, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Images.Add(image);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddImagesAsync(IEnumerable<Image> images, CancellationToken cancellationToken = default)
    {
        var list = images as IReadOnlyCollection<Image> ?? images.ToList();
        if (list.Count == 0)
            return;

        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.Images.AddRange(list);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetImageCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Images.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Image>> GetImagesAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Images.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Image>> GetImagesByTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Images.AsNoTracking()
            .Where(x => x.ScanTaskId == taskId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScanTask>> GetRecentScanTasksAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.ScanTasks.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(count)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteScanRecordsAsync(IReadOnlyList<long> imageIds, CancellationToken cancellationToken = default)
    {
        if (imageIds.Count == 0)
            return;

        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.Images.Where(x => imageIds.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.Images.RemoveRange(rows);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
