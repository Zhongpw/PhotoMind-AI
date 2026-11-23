using PhotoMind.Core.DTOs;
using PhotoMind.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PhotoMind.Infrastructure.ImageProcessing;

/// <summary>
/// Generates disposable, downscaled AI previews. It loads the ORIGINAL image, auto-orients it,
/// resizes to the configured maximum edge, and re-encodes to JPEG. The original file is never modified.
/// </summary>
public sealed class ImageNormalizer : IImageNormalizer
{
    private readonly IAppPaths _paths;

    public ImageNormalizer(IAppPaths paths) => _paths = paths;

    public async Task<AiPreview> CreatePreviewAsync(string sourceImagePath, AiConfig config, CancellationToken ct = default)
    {
        var targetPath = Path.Combine(_paths.TempDirectory, $"ai-preview-{Guid.NewGuid():N}.jpg");

        await Task.Run(() =>
        {
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
}
