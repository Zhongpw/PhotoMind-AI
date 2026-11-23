using PhotoMind.Core.DTOs;

namespace PhotoMind.Core.Interfaces;

/// <summary>Creates disposable, downscaled AI previews and never touches the original file.</summary>
public interface IImageNormalizer
{
    /// <summary>Loads, auto-orients, resizes and re-encodes an image to a temporary JPEG preview.</summary>
    Task<AiPreview> CreatePreviewAsync(string sourceImagePath, AiConfig config, CancellationToken ct = default);

    /// <summary>Best-effort delete of a temporary preview file.</summary>
    void DeletePreview(string tempFilePath);
}
