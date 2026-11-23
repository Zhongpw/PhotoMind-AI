namespace PhotoMind.Core.DTOs;

/// <summary>Read-only projection of a classification result joined with its image file name and category names, for the review UI.</summary>
public sealed class ClassificationResultSummary
{
    public long ImageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public string? CategoryName { get; set; }
    public string? SubCategoryName { get; set; }
    public double Confidence { get; set; }
    public bool NeedsReview { get; set; }
    public string? Description { get; set; }
    public string? Reason { get; set; }
    public string TagsJson { get; set; } = "[]";
}
