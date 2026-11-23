namespace PhotoMind.Core.Interfaces;

/// <summary>Generates and caches small preview thumbnails without touching the original file.</summary>
public interface IThumbnailService
{
    /// <summary>Absolute directory that holds cached thumbnails.</summary>
    string ThumbnailDirectory { get; }

    /// <summary>
    /// Returns the cached thumbnail path, generating it if absent. Returns null when the source cannot be decoded.
    /// The cache key is derived from path + size + last-write time so stale files are regenerated.
    /// </summary>
    Task<string?> GetOrCreateThumbnailAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes the cached thumbnail for a given source path, if any.</summary>
    void Invalidate(string imagePath);
}
