namespace PhotoMind.Core.DTOs;

/// <summary>
/// 撤销整理操作的结果。
/// </summary>
public sealed class OrganizeUndoResult
{
    public int RevertedCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public bool Success => FailedCount == 0;
}
