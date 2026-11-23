using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.FileSystem;

/// <summary>
/// 安全文件操作：复制（大小+SHA-256 校验）、移动（同盘重命名 / 跨盘先复制校验再删源）、磁盘空间检查。
/// </summary>
public sealed class SafeFileOperations
{
    private const int BufferSize = 81920;

    private readonly IFileHashService _hashService;

    public SafeFileOperations(IFileHashService hashService)
    {
        _hashService = hashService;
    }

    /// <summary>复制并校验（大小一致 + SHA-256 一致），校验失败时删除不完整副本并抛异常。</summary>
    public async Task CopyWithVerificationAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // FileMode.CreateNew 防止覆盖已存在文件（纵深防御；冲突应在调用前解析）。
        await using (var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true))
        await using (var dst = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
        {
            await src.CopyToAsync(dst, BufferSize, cancellationToken);
        }

        var sourceInfo = new FileInfo(source);
        var destInfo = new FileInfo(destination);
        if (sourceInfo.Length != destInfo.Length)
        {
            TryDelete(destination);
            throw new IOException($"复制后大小不一致（源 {sourceInfo.Length} 字节，目标 {destInfo.Length} 字节）：{destination}");
        }

        var sourceHash = await _hashService.ComputeSha256Async(source, cancellationToken);
        var destHash = await _hashService.ComputeSha256Async(destination, cancellationToken);
        if (!string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(destination);
            throw new IOException($"复制后 SHA-256 校验失败：{destination}");
        }
    }

    /// <summary>安全移动：同盘原子重命名；跨盘先复制校验、成功后才删除源（失败绝不删源）。</summary>
    public async Task MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var sourceRoot = Path.GetPathRoot(source);
        var destinationRoot = Path.GetPathRoot(destination);

        if (string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.Move(source, destination);
        }
        else
        {
            await CopyWithVerificationAsync(source, destination, cancellationToken);
            File.Delete(source);
        }
    }

    /// <summary>检查目标盘剩余空间是否足够。</summary>
    public static bool HasEnoughSpace(string destinationPath, long requiredBytes)
    {
        var root = Path.GetPathRoot(destinationPath);
        if (string.IsNullOrEmpty(root))
            return false;

        try
        {
            return new DriveInfo(root).AvailableFreeSpace >= requiredBytes;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 清理失败可忽略，不掩盖原始错误。
        }
    }
}
