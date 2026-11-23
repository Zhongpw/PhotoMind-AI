namespace PhotoMind.Core.DTOs;

/// <summary>
/// 整理计划中的单条预览项（含目标路径与冲突检测）。
/// </summary>
public sealed class OrganizePreviewItem
{
    public long ImageId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public string CategoryName { get; init; } = "其他";
    public string? SubCategoryName { get; init; }
    public string DestinationDirectory { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public bool DestinationExists { get; init; }
    public string? ErrorMessage { get; init; }
}
