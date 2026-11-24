namespace PhotoMind.Core.DTOs;

/// <summary>Aggregated statistics for CLIP candidate screening (from <see cref="Models.ClassificationResult"/>).</summary>
public sealed class CandidateStatsSummary
{
    /// <summary>Number of DeepSeek classification results where CLIP candidate screening was active.</summary>
    public int ScreenedCount { get; init; }

    /// <summary>Average number of candidates offered (over screened results).</summary>
    public double AverageCandidateCount { get; init; }

    /// <summary>Screened results whose final top-level category was inside the CLIP candidates.</summary>
    public int HitCount { get; init; }

    /// <summary>Screened results whose final top-level category was outside the CLIP candidates.</summary>
    public int MissCount { get; init; }
}
