using PhotoMind.Core.Enums;

namespace PhotoMind.Core.Models;

/// <summary>
/// A discovered photo file and its locally-extracted metadata.
/// OriginalPath is immutable once set: classification/organisation must never rewrite it.
/// </summary>
public sealed class Image
{
    public long Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>Absolute path of the ORIGINAL file. Never modified by classification or organisation.</summary>
    public string OriginalPath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    /// <summary>SHA-256 of the original file bytes (hex, lowercase).</summary>
    public string FileHash { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }

    public ImageFormat Format { get; set; } = ImageFormat.Unknown;

    /// <summary>File creation time reported by the filesystem.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>File last-write time reported by the filesystem.</summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>EXIF capture time, when available. Never uploaded to the AI by default.</summary>
    public DateTime? ExifDate { get; set; }

    public ImageScanStatus ScanStatus { get; set; } = ImageScanStatus.Pending;

    /// <summary>Row creation timestamp (database bookkeeping).</summary>
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    /// <summary>Optional link to the scan task that discovered this image.</summary>
    public long? ScanTaskId { get; set; }

    /// <summary>Absolute path of the locally cached thumbnail, when one was generated. Never the original file.</summary>
    public string? ThumbnailPath { get; set; }
}
