namespace PhotoMind.Core.DTOs;

/// <summary>A local CLIP classification decision (before persistence).</summary>
public sealed class ClipMatchResult
{
    /// <summary>Resolved top-level category id, or null when none matched confidently.</summary>
    public long? CategoryId { get; set; }

    /// <summary>Resolved sub-category id, or null.</summary>
    public long? SubCategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;
    public string SubCategoryName { get; set; } = string.Empty;

    /// <summary>Top-level softmax probability of the winning category (the token-saving gate uses this).</summary>
    public double Confidence { get; set; }

    /// <summary>Softmax probability of the winning sub-category within its parent (informational).</summary>
    public double SubCategoryConfidence { get; set; }

    /// <summary>Raw CLIP image embedding (L2-normalized) used for this decision; null when inference failed.</summary>
    public float[]? ImageEmbedding { get; set; }
}
