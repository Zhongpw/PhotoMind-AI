using Microsoft.EntityFrameworkCore;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure.Database;

namespace PhotoMind.Infrastructure.Services;

/// <summary>
/// Marks tasks left in a non-terminal state (Pending/Running/Paused) as Failed at startup, so an
/// abnormal exit never leaves a "zombie" task that the UI would report as still in progress.
/// Original photos are never touched.
/// </summary>
public sealed class TaskRecoveryService : ITaskRecoveryService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public TaskRecoveryService(IDbContextFactory<AppDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<TaskRecoveryResult> RecoverInterruptedTasksAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;

        var scanTasks = await db.ScanTasks
            .Where(t => t.Status == TaskState.Pending || t.Status == TaskState.Running || t.Status == TaskState.Paused)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var classificationTasks = await db.ClassificationTasks
            .Where(t => t.Status == TaskState.Pending || t.Status == TaskState.Running || t.Status == TaskState.Paused)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var organizeTasks = await db.OrganizeTasks
            .Where(t => t.Status == TaskState.Pending || t.Status == TaskState.Running || t.Status == TaskState.Paused)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var task in scanTasks) { task.Status = TaskState.Failed; task.FinishedAt = now; }
        foreach (var task in classificationTasks) { task.Status = TaskState.Failed; task.FinishedAt = now; }
        foreach (var task in organizeTasks) { task.Status = TaskState.Failed; task.FinishedAt = now; }

        if (scanTasks.Count > 0 || classificationTasks.Count > 0 || organizeTasks.Count > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TaskRecoveryResult
        {
            RecoveredScanTasks = scanTasks.Count,
            RecoveredClassificationTasks = classificationTasks.Count,
            RecoveredOrganizeTasks = organizeTasks.Count,
        };
    }
}
