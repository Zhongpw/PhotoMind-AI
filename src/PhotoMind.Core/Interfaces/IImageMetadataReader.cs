using PhotoMind.Core.Enums;

namespace PhotoMind.Core.Interfaces;

/// <summary>Reads pixel dimensions and (optionally) EXIF capture time from an image file.</summary>
public interface IImageMetadataReader
{
    /// <summary>Returns (width, height, exifDate). Throws ImageReadException when the file cannot be decoded.</summary>
    Task<ImageMetadata> ReadAsync(string filePath, ImageFormat format, CancellationToken cancellationToken = default);
}

/// <summary>Lightweight metadata result.</summary>
public readonly record struct ImageMetadata(int Width, int Height, DateTime? ExifDate);
