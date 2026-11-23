namespace PhotoMind.Core.DTOs;

/// <summary>Validated, sanitized classification output parsed from the AI response.</summary>
public sealed class AiRecognitionResult
{
    /// <summary>Top-level category name (sanitized). Empty means "unclassified".</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Optional second-level category name (sanitized).</summary>
    public string? Subcategory { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

    public string Description { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    /// <summary>Confidence in [0, 1].</summary>
    public double Confidence { get; set; }

    /// <summary>True when the result needs human review (low confidence, uncertain, or new-category proposal).</summary>
    public bool NeedsReview { get; set; }

    /// <summary>New top-level category proposed by the AI (auto/hybrid mode only).</summary>
    public string? SuggestedCategory { get; set; }

    public bool IsUnclassified => string.IsNullOrWhiteSpace(Category) || Category == "未分类";
}
