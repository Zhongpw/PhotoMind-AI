using System.Security.Cryptography;
using System.Text;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.ImageProcessing;

/// <summary>
/// Generates small, cached JPEG previews. The cache key is path + size + last-write time, so an edited
/// source file automatically regenerates its thumbnail. Never modifies the original file.
/// </summary>
public sealed class ThumbnailService : IThumbnailService
{
    private readonly IAppPaths _paths;
    private readonly IImageFormatDetector _detector;
    private readonly int _maxSize;

    /// <summary>Bounds simultaneous image decodes so a large scan never floods memory with concurrent Bitmap loads.</summary>
    private static readonly SemaphoreSlim _decodeGate = new(Math.Max(1, Environment.ProcessorCount));

    public ThumbnailService(IAppPaths paths, IImageFormatDetector detector, int maxSize = 256)
    {
        _paths = paths;
        _detector = detector;
        _maxSize = maxSize;
        Directory.CreateDirectory(paths.ThumbnailDirectory);
    }

    public string ThumbnailDirectory => _paths.ThumbnailDirectory;

    public async Task<string?> GetOrCreateThumbnailAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var target = GetThumbnailPath(imagePath);
        if (File.Exists(target))
            return target;

        await _decodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    var format = DetectFormat(imagePath);
                    if (format is ImageFormat.Tiff or ImageFormat.Heic or ImageFormat.Raw)
                    {
                        // Extended formats are decoded by Magick.NET (ImageSharp cannot read HEIC/RAW
                        // and only partially supports TIFF variants).
                        return MagickImageDecoder.TryCreateJpeg(imagePath, target, _maxSize, 80)
                            ? (string?)target
                            : null;
                    }

                    using var image = Image.Load(imagePath);
                    image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(_maxSize, _maxSize),
                    }));
                    image.SaveAsJpeg(target);
                    return (string?)target;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.Debug(ex, "缩略图生成失败（原始文件不受影响）: {Path}", imagePath);
                    return null;
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    public void Invalidate(string imagePath)
    {
        var target = GetThumbnailPath(imagePath);
        try
        {
            if (File.Exists(target))
                File.Delete(target);
        }
        catch
        {
            // Best-effort cache invalidation only; a failed delete must never surface to the user.
        }
    }

    private ImageFormat DetectFormat(string imagePath)
    {
        try
        {
            using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 16);
            Span<byte> buffer = stackalloc byte[16];
            var read = fs.Read(buffer);
            return _detector.Detect(buffer[..read]);
        }
        catch
        {
            return ImageFormat.Unknown;
        }
    }

    private string GetThumbnailPath(string imagePath)
    {
        var fi = new FileInfo(imagePath);
        var key = $"{fi.FullName}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(ThumbnailDirectory, hash + ".jpg");
    }
}
