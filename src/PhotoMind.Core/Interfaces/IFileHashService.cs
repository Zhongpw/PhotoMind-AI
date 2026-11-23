using PhotoMind.Core.Enums;

namespace PhotoMind.Core.Interfaces;

/// <summary>Computes content hashes used for dedup detection and organise verification.</summary>
public interface IFileHashService
{
    Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default);
}
