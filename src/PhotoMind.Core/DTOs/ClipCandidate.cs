namespace PhotoMind.Core.DTOs;

/// <summary>
/// A single category surfaced by local CLIP top-K screening. A candidate may be a top-level category
/// or a sub-category; the parent fields let the caller reconstruct the full branch path for the prompt.
/// </summary>
public sealed class ClipCandidate
{
    /// <summary>Category id (top-level or sub-category node).</summary>
    public long CategoryId { get; set; }

    /// <summary>Chinese display name of the category.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Parent category id; null for a top-level category.</summary>
    public long? ParentId { get; set; }

    /// <summary>Chinese display name of the parent (empty for a top-level category).</summary>
    public string? ParentName { get; set; }

    /// <summary>Raw cosine similarity between the image embedding and this category's CLIP text embedding.</summary>
    public double Similarity { get; set; }
}
