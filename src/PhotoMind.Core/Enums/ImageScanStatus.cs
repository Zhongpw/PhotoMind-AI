namespace PhotoMind.Core.Enums;

/// <summary>Per-image scan result status (set during a scan task).</summary>
public enum ImageScanStatus
{
    Pending = 0,
    Scanned = 1,
    Failed = 2,
    Skipped = 3,
}
