using System.Runtime.CompilerServices;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.FileSystem;

/// <summary>
/// Async directory enumeration for image candidates. It never loads all paths into memory and skips
/// protected system directories, unreadable directories, the app's own data directory, excluded
/// directories and reparse points (symbolic links) so that recursion loops are avoided.
/// </summary>
public sealed class DirectoryScanner
{
    private readonly IImageFormatDetector _detector;
    private readonly string _dataDirectory;

    private static readonly string[] ProtectedDirectoryNames =
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData",
        "$Recycle.Bin", "$RECYCLE.BIN", "System Volume Information", "Recovery",
        "PerfLogs", "MSOCache", "WinSxS", "Boot",
    };

    public DirectoryScanner(IImageFormatDetector detector, IAppPaths paths)
    {
        _detector = detector;
        _dataDirectory = Path.GetFullPath(paths.DataDirectory);
    }

    /// <summary>Fast extension-based count of candidate files (no content read), for the pre-scan estimate.</summary>
    public async Task<long> EstimateCountAsync(
        IReadOnlyList<string> roots,
        IReadOnlyCollection<string> excludedDirectories,
        bool recursive,
        CancellationToken cancellationToken = default)
    {
        long count = 0;
        await foreach (var _ in EnumerateCandidateFilesAsync(roots, excludedDirectories, recursive, cancellationToken)
                           .ConfigureAwait(false))
        {
            if ((++count & 0x3FF) == 0)
                cancellationToken.ThrowIfCancellationRequested();
        }
        return count;
    }

    public async IAsyncEnumerable<string> EnumerateCandidateFilesAsync(
        IReadOnlyList<string> roots,
        IReadOnlyCollection<string> excludedDirectories,
        bool recursive,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var excludes = NormalizePaths(excludedDirectories);

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            string fullRoot;
            try { fullRoot = Path.GetFullPath(root.Trim()); }
            catch { continue; }

            if (File.Exists(fullRoot))
            {
                if (IsCandidate(fullRoot))
                    yield return fullRoot;
                continue;
            }

            if (!Directory.Exists(fullRoot))
                continue;

            await foreach (var file in EnumerateDirectoryAsync(fullRoot, excludes, recursive, cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return file;
            }
        }
    }

    private async IAsyncEnumerable<string> EnumerateDirectoryAsync(
        string directory,
        HashSet<string> excludes,
        bool recursive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (IsProtectedOrSkipped(directory, excludes))
            yield break;

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(directory); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            yield break; // unreadable directory: skip it, do not abort the whole task
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCandidate(file))
                yield return file;
        }

        if (!recursive)
            yield break;

        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(directory); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            yield break;
        }

        foreach (var sub in dirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSymbolicLink(sub) || IsProtectedOrSkipped(sub, excludes))
                continue;

            await foreach (var file in EnumerateDirectoryAsync(sub, excludes, recursive, cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return file;
            }
        }
    }

    private bool IsCandidate(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return _detector.CandidateExtensions.Contains(ext);
    }

    private static bool IsSymbolicLink(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return false; }
    }

    private bool IsProtectedOrSkipped(string directory, HashSet<string> excludes)
    {
        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (excludes.Contains(full))
            return true;

        // Never recurse into our own data/cache/log directory (avoids re-scanning cached thumbnails).
        if (string.Equals(full, _dataDirectory, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(_dataDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return true;

        // System-protected directories are only skipped when they sit directly at a drive root.
        if (IsAtDriveRoot(full))
        {
            var name = Path.GetFileName(full);
            foreach (var protectedName in ProtectedDirectoryNames)
            {
                if (string.Equals(name, protectedName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            if (name.StartsWith('$'))
                return true;
        }

        return false;
    }

    private static bool IsAtDriveRoot(string full)
    {
        var parent = Path.GetDirectoryName(full);
        return parent is not null
               && parent.Length == 3
               && parent[1] == ':'
               && (parent[2] == '\\' || parent[2] == '/');
    }

    private static HashSet<string> NormalizePaths(IReadOnlyCollection<string> paths)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p))
                continue;
            try { set.Add(Path.GetFullPath(p.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); }
            catch { /* ignore invalid path */ }
        }
        return set;
    }
}
