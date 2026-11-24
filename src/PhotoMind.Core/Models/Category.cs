namespace PhotoMind.Core.Models;

/// <summary>A node in the (possibly multi-level) category tree.</summary>
public sealed class Category
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional English alias used as the CLIP text prompt (CLIP understands English far better than Chinese).</summary>
    public string? Alias { get; set; }

    /// <summary>Parent category id; null for a top-level category.</summary>
    public long? ParentId { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>True for built-in categories the user cannot delete (they may still rename them).</summary>
    public bool IsSystem { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
