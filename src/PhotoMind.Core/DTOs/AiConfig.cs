using PhotoMind.Core.Constants;
using PhotoMind.Core.Enums;

namespace PhotoMind.Core.DTOs;

/// <summary>
/// Non-secret AI settings persisted to a local JSON config file. The API key is NOT stored here;
/// it lives in the DPAPI-backed <see cref="Interfaces.ISecretStore"/>.
/// </summary>
public sealed class AiConfig
{
    public string BaseUrl { get; set; } = AiConstants.DefaultBaseUrl;

    public string Model { get; set; } = AiConstants.DefaultModel;

    public AiDetailLevel Detail { get; set; } = AiDetailLevel.Low;

    /// <summary>Concurrent request count (clamped to <see cref="AiConstants.MinConcurrency"/>..<see cref="AiConstants.MaxConcurrency"/>).</summary>
    public int Concurrency { get; set; } = AiConstants.DefaultConcurrency;

    /// <summary>Maximum retries per image (non-negative).</summary>
    public int MaxRetries { get; set; } = AiConstants.DefaultMaxRetries;

    /// <summary>Privacy gate: when false, no image is ever uploaded to the AI.</summary>
    public bool AllowUpload { get; set; }

    /// <summary>Maximum edge of the AI preview image in pixels.</summary>
    public int MaxPreviewDimension { get; set; } = AiConstants.DefaultMaxPreviewDimension;

    /// <summary>JPEG quality (1..100) for the generated AI preview.</summary>
    public int PreviewQuality { get; set; } = AiConstants.DefaultPreviewQuality;

    /// <summary>Normalizes user input so invalid values never reach the request layer.</summary>
    public AiConfig Sanitized()
    {
        Concurrency = Math.Clamp(Concurrency, AiConstants.MinConcurrency, AiConstants.MaxConcurrency);
        MaxRetries = Math.Clamp(MaxRetries, 0, 10);
        MaxPreviewDimension = Math.Clamp(MaxPreviewDimension, 128, 4096);
        PreviewQuality = Math.Clamp(PreviewQuality, 10, 100);
        if (string.IsNullOrWhiteSpace(BaseUrl))
            BaseUrl = AiConstants.DefaultBaseUrl;
        if (string.IsNullOrWhiteSpace(Model))
            Model = AiConstants.DefaultModel;
        return this;
    }
}
