using System.Security.Cryptography;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.ImageProcessing;

/// <summary>Streaming SHA-256 over a file's bytes (used for dedup and organise verification).</summary>
public sealed class FileHashService : IFileHashService
{
    public async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite, // tolerate files that are open elsewhere, we only read
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
