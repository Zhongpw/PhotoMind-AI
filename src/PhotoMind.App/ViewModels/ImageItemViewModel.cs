using PhotoMind.Core.Models;

namespace PhotoMind.App.ViewModels;

/// <summary>Lightweight, read-only item bound by the virtualised thumbnail grid.</summary>
public sealed class ImageItemViewModel
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB" };

    public ImageItemViewModel(Image image)
    {
        Id = image.Id;
        FileName = image.FileName;
        OriginalPath = image.OriginalPath;
        ThumbnailPath = image.ThumbnailPath;
        HasThumbnail = !string.IsNullOrEmpty(ThumbnailPath);
        FileSizeText = FormatSize(image.FileSize);
        DimensionsText = image.Width > 0 && image.Height > 0 ? $"{image.Width} × {image.Height}" : "-";
        IsFailed = image.ScanStatus == Core.Enums.ImageScanStatus.Failed;
    }

    public long Id { get; }
    public string FileName { get; }
    public string OriginalPath { get; }
    public string? ThumbnailPath { get; }
    public bool HasThumbnail { get; }
    public string FileSizeText { get; }
    public string DimensionsText { get; }
    public bool IsFailed { get; }

    private static string FormatSize(long bytes)
    {
        double value = bytes;
        int suffix = 0;
        while (value >= 1024 && suffix < SizeSuffixes.Length - 1)
        {
            value /= 1024;
            suffix++;
        }
        return suffix == 0 ? $"{value:0} {SizeSuffixes[suffix]}" : $"{value:0.##} {SizeSuffixes[suffix]}";
    }
}
