using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PhotoMind.Infrastructure.ImageProcessing;

/// <summary>
/// Generates disposable, downscaled AI previews. It loads the ORIGINAL image, auto-orients it,
/// resizes to the configured maximum edge, and re-encodes to JPEG. The original file is never modified.
/// TIFF/HEIC/RAW are decoded by Magick.NET; all other formats use ImageSharp.
/// </summary>
public sealed class ImageNormalizer : IImageNormalizer
{
    private readonly IAppPaths _paths;
    private readonly IImageFormatDetector _detector;

    public ImageNormalizer(IAppPaths paths, IImageFormatDetector detector)
    {
        _paths = paths;
        _detector = detector;
    }

    public async Task<AiPreview> CreatePreviewAsync(string sourceImagePath, AiConfig config, CancellationToken ct = default)
    {
        var targetPath = Path.Combine(_paths.TempDirectory, $"ai-preview-{Guid.NewGuid():N}.jpg");

        await Task.Run(() =>
        {
            var format = DetectFormat(sourceImagePath);
            if (format is ImageFormat.Tiff or ImageFormat.Heic or ImageFormat.Raw)
            {
                if (!MagickImageDecoder.TryCreateJpeg(sourceImagePath, targetPath, config.MaxPreviewDimension, config.PreviewQuality))
                    throw new ImageReadException("无法解码图片以生成 AI 预览", sourceImagePath);
                return;
            }

            using var image = Image.Load(sourceImagePath);
            image.Mutate(ctx => ctx
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(config.MaxPreviewDimension, config.MaxPreviewDimension),
                }));
            image.SaveAsJpeg(targetPath, new JpegEncoder { Quality = config.PreviewQuality });
        }, ct).ConfigureAwait(false);

        return new AiPreview(targetPath, "image/jpeg");
    }

    public void DeletePreview(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
        catch
        {
            // Best-effort cleanup only; a leaked temp file is harmless and cleaned up later.
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
}
