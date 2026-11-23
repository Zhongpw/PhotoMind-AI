using PhotoMind.Core.Models;

namespace PhotoMind.Core.DTOs;

/// <summary>
/// 一组 SHA-256 完全相同的图片（重复图片）。仅用于提示，绝不默认删除。
/// </summary>
public sealed class DuplicateImageGroup
{
    public string FileHash { get; init; } = string.Empty;
    public IReadOnlyList<Image> Images { get; init; } = Array.Empty<Image>();
    public int Count => Images.Count;
}
