namespace PhotoMind.Core.DTOs;

/// <summary>Filter criteria for the review (分类审核) page result list.</summary>
public sealed class ReviewFilter
{
    /// <summary>Top-level category id; null matches all.</summary>
    public long? CategoryId { get; set; }

    /// <summary>Sub-category id; null matches all.</summary>
    public long? SubCategoryId { get; set; }

    /// <summary>Minimum confidence in [0, 1]; null matches all.</summary>
    public double? MinConfidence { get; set; }

    /// <summary>When true, only rows flagged <c>NeedsReview</c> are returned.</summary>
    public bool NeedsReviewOnly { get; set; }

    /// <summary>Free-text match against file name or AI tags.</summary>
    public string? Search { get; set; }
}
