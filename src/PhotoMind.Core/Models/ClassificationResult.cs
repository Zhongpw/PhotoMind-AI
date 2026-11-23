namespace PhotoMind.Core.Models;

/// <summary>The AI's classification output for a single image.</summary>
public sealed class ClassificationResult
{
    public long Id { get; set; }

    public long ImageId { get; set; }

    public long? CategoryId { get; set; }
    public long? SubCategoryId { get; set; }

    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    /// <summary>AI confidence in [0, 1].</summary>
    public double Confidence { get; set; }

    /// <summary>JSON-encoded string array of tags.</summary>
    public string TagsJson { get; set; } = "[]";

    public bool NeedsReview { get; set; }

    public string ModelName { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
