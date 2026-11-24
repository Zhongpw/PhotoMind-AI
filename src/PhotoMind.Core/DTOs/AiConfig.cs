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

    /// <summary>Token-saving mode: route high-confidence images through local CLIP before DeepSeek.</summary>
    public bool SaveTokenMode { get; set; }

    /// <summary>NVIDIA GPU acceleration for local CLIP inference (falls back to CPU when unavailable).</summary>
    public bool GpuAcceleration { get; set; }

    /// <summary>Temperature used for CLIP softmax normalization (lower = sharper distribution).</summary>
    public double ClipTemperature { get; set; } = AiConstants.DefaultClipTemperature;

    /// <summary>CLIP softmax confidence above which a result is accepted without a DeepSeek request.</summary>
    public double ClipConfidenceThreshold { get; set; } = AiConstants.DefaultClipConfidenceThreshold;

    /// <summary>Logistic-calibration slope for CLIP top1/top2 margin (0 = use raw softmax confidence).</summary>
    public double ClipCalibrationSlope { get; set; } = AiConstants.DefaultClipCalibrationSlope;

    /// <summary>Logistic-calibration intercept for CLIP top1/top2 margin.</summary>
    public double ClipCalibrationIntercept { get; set; } = AiConstants.DefaultClipCalibrationIntercept;

    /// <summary>Normalizes user input so invalid values never reach the request layer.</summary>
    public AiConfig Sanitized()
    {
        Concurrency = Math.Clamp(Concurrency, AiConstants.MinConcurrency, AiConstants.MaxConcurrency);
        MaxRetries = Math.Clamp(MaxRetries, 0, 10);
        MaxPreviewDimension = Math.Clamp(MaxPreviewDimension, 128, 4096);
        PreviewQuality = Math.Clamp(PreviewQuality, 10, 100);
        ClipTemperature = Math.Clamp(ClipTemperature, 0.01, 1.0);
        ClipConfidenceThreshold = Math.Clamp(ClipConfidenceThreshold, 0.0, 1.0);
        ClipCalibrationSlope = Math.Clamp(ClipCalibrationSlope, 0.0, 10000.0);
        ClipCalibrationIntercept = Math.Clamp(ClipCalibrationIntercept, -100.0, 100.0);
        if (string.IsNullOrWhiteSpace(BaseUrl))
            BaseUrl = AiConstants.DefaultBaseUrl;
        if (string.IsNullOrWhiteSpace(Model))
            Model = AiConstants.DefaultModel;
        return this;
    }
}
