namespace PhotoMind.Core.DTOs;

/// <summary>
/// 整理前预览（来源/目标/总数/分类数/文件夹数/磁盘占用/冲突数）。
/// </summary>
public sealed class OrganizePreview
{
    public IReadOnlyList<OrganizePreviewItem> Items { get; init; } = Array.Empty<OrganizePreviewItem>();

    public int TotalCount => Items.Count;
    public int CategoryCount => Items.Select(i => i.CategoryName).Distinct(StringComparer.Ordinal).Count();
    public int FolderCount => Items.Select(i => i.DestinationDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public long TotalBytes => Items.Where(i => i.ErrorMessage is null).Sum(i => i.FileSize);
    public int ConflictCount => Items.Count(i => i.DestinationExists);
    public int ErrorCount => Items.Count(i => i.ErrorMessage is not null);
}
