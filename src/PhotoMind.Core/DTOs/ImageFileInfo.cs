using PhotoMind.Core.Enums;

namespace PhotoMind.Core.DTOs;

/// <summary>A discovered image file plus locally extracted metadata (before persistence).</summary>
public sealed record ImageFileInfo
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public long FileSize { get; init; }
    public ImageFormat Format { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ModifiedAt { get; init; }
    public DateTime? ExifDate { get; init; }
}
