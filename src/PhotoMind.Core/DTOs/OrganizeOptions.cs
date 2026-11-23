using PhotoMind.Core.Enums;

namespace PhotoMind.Core.DTOs;

/// <summary>
/// 照片整理操作的配置。
/// </summary>
public sealed class OrganizeOptions
{
    /// <summary>目标根目录（照片将按分类复制/移动到其下的分类子文件夹）。</summary>
    public string DestinationPath { get; init; } = string.Empty;

    /// <summary>整理操作类型。默认复制（安全优先，禁止默认移动）。</summary>
    public OrganizeOperationType OperationType { get; init; } = OrganizeOperationType.Copy;

    /// <summary>要整理的照片 Id；为空表示整理全部已分类照片。</summary>
    public IReadOnlyList<long> ImageIds { get; init; } = Array.Empty<long>();
}
