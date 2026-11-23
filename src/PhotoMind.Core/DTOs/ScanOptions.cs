using PhotoMind.Core.Enums;

namespace PhotoMind.Core.DTOs;

/// <summary>User-configurable scan parameters. Serialisable so it can be persisted with the task.</summary>
public sealed class ScanOptions
{
    /// <summary>Root directories to scan.</summary>
    public List<string> SourcePaths { get; set; } = new();

    /// <summary>Directories (absolute) to skip even when nested under a source root.</summary>
    public List<string> ExcludedDirectories { get; set; } = new();

    /// <summary>Whether to recurse into subdirectories.</summary>
    public bool Recursive { get; set; } = true;

    /// <summary>Formats to accept. Empty means all supported formats.</summary>
    public List<ImageFormat> AllowedFormats { get; set; } = new();

    /// <summary>Follow directory symbolic links (false avoids cycles).</summary>
    public bool FollowSymbolicLinks { get; set; }
}
