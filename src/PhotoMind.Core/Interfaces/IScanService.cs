using PhotoMind.Core.DTOs;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;

namespace PhotoMind.Core.Interfaces;

/// <summary>Orchestrates a full directory/image scan, persisting results and reporting progress.</summary>
public interface IScanService
{
    /// <summary>
    /// Scans the given roots, detects images by content, extracts metadata, computes hashes,
    /// generates thumbnails and persists every image plus the task record.
    /// Returns the persisted <see cref="ScanTask"/> when finished.
    /// </summary>
    Task<ScanTask> ScanAsync(
        ScanOptions options,
        PauseToken pauseToken,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
