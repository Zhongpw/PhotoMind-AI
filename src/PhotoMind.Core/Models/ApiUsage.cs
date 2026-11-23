namespace PhotoMind.Core.Models;

/// <summary>API usage/cost record. EstimatedCost is an estimate when IsEstimated is true.</summary>
public sealed class ApiUsage
{
    public long Id { get; set; }

    public long? TaskId { get; set; }

    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }

    public decimal EstimatedCost { get; set; }

    /// <summary>True when token counts are estimates (no Usage field returned by the API).</summary>
    public bool IsEstimated { get; set; }

    public string RequestStatus { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
