namespace PhotoMind.Core.DTOs;

/// <summary>One scan error that should be surfaced to the user but not abort the task.</summary>
public sealed record ScanError(string FullPath, string Message);
