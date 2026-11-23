using PhotoMind.Core.Enums;

namespace PhotoMind.Core.DTOs;

/// <summary>Request options for one AI classification run.</summary>
public sealed class AiClassifyOptions
{
    /// <summary>Explicit image ids to classify; empty means "all not-yet-classified images".</summary>
    public IReadOnlyList<long> ImageIds { get; set; } = Array.Empty<long>();

    public ClassificationMode Mode { get; set; } = ClassificationMode.Auto;

    /// <summary>When true, already-classified images are re-run (otherwise skipped).</summary>
    public bool IncludeAlreadyClassified { get; set; }
}
