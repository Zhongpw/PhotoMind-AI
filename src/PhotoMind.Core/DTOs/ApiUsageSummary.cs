namespace PhotoMind.Core.DTOs;

/// <summary>API 用量聚合统计。</summary>
public sealed class ApiUsageSummary
{
    public int RequestCount { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens { get; init; }
    public decimal EstimatedCost { get; init; }

    /// <summary>使用估算 Token 值（而非 API 返回的真实值）的记录数。</summary>
    public int EstimatedCount { get; init; }
}
