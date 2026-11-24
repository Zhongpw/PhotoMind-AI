using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Models;

namespace PhotoMind.Core.Interfaces;

/// <summary>
/// Local CLIP (ONNX) inference: loads the ViT-B/32 model, encodes images and text into normalized
/// embeddings, and matches images against the category tree. Inference is CPU-only by default;
/// CUDA acceleration is layered on top in a later stage with automatic CPU fallback.
/// </summary>
public interface IClipModelService
{
    /// <summary>Current model load state.</summary>
    ClipModelState State { get; }

    /// <summary>Device actually used for inference.</summary>
    InferenceDevice Device { get; }

    /// <summary>Human-readable device description (e.g. "NVIDIA GeForce RTX 3060" or "CPU").</summary>
    string DeviceDescription { get; }

    /// <summary>Last load/inference error, for diagnostics and the UI.</summary>
    string? LastError { get; }

    /// <summary>True when the requested device failed and inference degraded to CPU.</summary>
    bool Degraded { get; }

    /// <summary>Reason for the degradation (empty when not degraded).</summary>
    string? DegradeReason { get; }

    /// <summary>Loads the model if not already loaded. Idempotent; throws <see cref="Exceptions.PhotoMindException"/> on failure.</summary>
    Task EnsureLoadedAsync(CancellationToken ct = default);

    /// <summary>Encodes an image file (decoded via ImageSharp/Magick.NET) into an L2-normalized embedding.</summary>
    Task<float[]> EncodeImageAsync(string imagePath, CancellationToken ct = default);

    /// <summary>Encodes text into an L2-normalized embedding (uses the persistent text cache).</summary>
    Task<float[]> EncodeTextAsync(string text, CancellationToken ct = default);

    /// <summary>Pre-encodes every category label so the first classification run is fast.</summary>
    Task WarmupAsync(IReadOnlyList<Category> categories, CancellationToken ct = default);

    /// <summary>
    /// Matches an image embedding against the category tree (top-level first, then sub-category),
    /// returning the winning labels and temperature-normalized softmax confidence.
    /// </summary>
    Task<ClipMatchResult> MatchAsync(
        float[] imageEmbedding,
        IReadOnlyList<Category> categories,
        double temperature,
        CancellationToken ct = default);
}
