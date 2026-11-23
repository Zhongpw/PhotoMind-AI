using PhotoMind.Core.Exceptions;

namespace PhotoMind.Infrastructure.FileSystem;

/// <summary>
/// 整理路径合法性校验与分类文件夹名过滤（防路径穿越/非法字符/系统保护目录）。
/// </summary>
public static class PathSafety
{
    private static readonly HashSet<string> ProtectedTopLevelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData",
        "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs",
        "MSOCache", "WinSxS", "Boot",
    };

    /// <summary>把分类名过滤为合法的文件夹名（去除非法字符、Windows 保留名，防路径穿越）。</summary>
    public static string SanitizeFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "其他";

        var trimmed = name.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            builder.Append(invalid.Contains(ch) ? '_' : ch);

        var result = builder.ToString().Trim().TrimEnd('.', ' ');

        // Windows 保留设备名（CON/PRN/AUX/NUL/COM1-9/LPT1-9）。
        var stem = result.Split('.')[0];
        if (IsReservedDeviceName(stem))
            result = "_" + result;

        return string.IsNullOrWhiteSpace(result) ? "其他" : result;
    }

    /// <summary>校验整理目标目录：必须为绝对路径、非盘根、非系统保护目录。</summary>
    public static void ValidateDestination(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new PathValidationException("目标目录不能为空。");

        if (!Path.IsPathRooted(destinationPath))
            throw new PathValidationException("目标目录必须是绝对路径。");

        var full = Path.GetFullPath(destinationPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetPathRoot(full);
        if (root is not null &&
            string.Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), full, StringComparison.OrdinalIgnoreCase))
            throw new PathValidationException("不能把磁盘根目录作为整理目标，请选择一个具体的子文件夹。");

        var parts = full.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        // parts[0] = 盘符（如 "C:"）；parts[1] 起为目录名。
        if (parts.Length >= 2 && ProtectedTopLevelNames.Contains(parts[1]))
            throw new PathValidationException("不能写入系统保护目录。");
    }

    private static bool IsReservedDeviceName(string name)
    {
        var upper = name.ToUpperInvariant();
        if (upper is "CON" or "PRN" or "AUX" or "NUL")
            return true;
        if (upper.Length == 4 && (upper.StartsWith("COM", StringComparison.Ordinal) || upper.StartsWith("LPT", StringComparison.Ordinal)))
            return upper[3] is >= '1' and <= '9';
        return false;
    }
}
