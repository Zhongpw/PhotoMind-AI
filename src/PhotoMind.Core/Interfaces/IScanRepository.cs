using PhotoMind.Core.Models;

namespace PhotoMind.Core.Interfaces;

/// <summary>Persistence for scan tasks and discovered images.</summary>
public interface IScanRepository
{
    Task<ScanTask> CreateTaskAsync(ScanTask task, CancellationToken cancellationToken = default);
    Task<ScanTask?> GetTaskAsync(long taskId, CancellationToken cancellationToken = default);
    Task UpdateTaskAsync(ScanTask task, CancellationToken cancellationToken = default);

    Task AddImageAsync(Image image, CancellationToken cancellationToken = default);
    Task AddImagesAsync(IEnumerable<Image> images, CancellationToken cancellationToken = default);

    Task<int> GetImageCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Image>> GetImagesAsync(int skip, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Image>> GetImagesByTaskAsync(long taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanTask>> GetRecentScanTasksAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Deletes cached thumbnails and scan records for the given image set (never touches originals).</summary>
    Task DeleteScanRecordsAsync(IReadOnlyList<long> imageIds, CancellationToken cancellationToken = default);
}
