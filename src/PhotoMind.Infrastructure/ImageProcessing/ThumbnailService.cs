using System.Security.Cryptography;
using System.Text;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.ImageProcessing;

/// <summary>
/// Generates small, cached JPEG previews. The cache key is path + size + last-write time, so an edited
/// source file automatically regenerates its thumbnail. Never modifies the original file.
/// </summary>
public sealed class ThumbnailService : IThumbnailService
{
    private readonly IAppPaths _paths;
    private readonly int _maxSize;

    public ThumbnailService(IAppPaths paths, int maxSize = 256)
    {
        _paths = paths;
        _maxSize = maxSize;
        Directory.CreateDirectory(paths.ThumbnailDirectory);
    }

    public string ThumbnailDirectory => _paths.ThumbnailDirectory;

    public Task<string?> GetOrCreateThumbnailAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var target = GetThumbnailPath(imagePath);
        if (File.Exists(target))
            return Task.FromResult<string?>(target);

        return Task.Run(() =>
        {
            try
            {
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
        }, cancellationToken);
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

    private string GetThumbnailPath(string imagePath)
    {
        var fi = new FileInfo(imagePath);
        var key = $"{fi.FullName}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(ThumbnailDirectory, hash + ".jpg");
    }
}
