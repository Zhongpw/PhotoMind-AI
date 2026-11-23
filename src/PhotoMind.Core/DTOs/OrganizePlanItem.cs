namespace PhotoMind.Core.DTOs;

/// <summary>
/// 整理计划中的单条照片（来自数据库 join：图片 + 分类结果 + 分类名）。
/// </summary>
public sealed class OrganizePlanItem
{
    public long ImageId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string FileHash { get; init; } = string.Empty;
    public long? CategoryId { get; init; }
    public long? SubCategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? SubCategoryName { get; init; }
}
