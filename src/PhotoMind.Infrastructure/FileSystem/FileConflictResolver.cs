namespace PhotoMind.Infrastructure.FileSystem;

/// <summary>
/// 同名文件冲突解析：photo.jpg → photo_1.jpg → photo_2.jpg …（绝不覆盖已有文件）。
/// </summary>
public static class FileConflictResolver
{
    public static string GetUniquePath(string destinationPath)
    {
        if (!File.Exists(destinationPath))
            return destinationPath;

        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);

        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(directory, $"{name}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }
}
