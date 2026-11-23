using PhotoMind.Core.DTOs;

namespace PhotoMind.Core.Interfaces;

/// <summary>Loads/saves non-secret AI settings from a local JSON config file.</summary>
public interface IAiConfigStore
{
    Task<AiConfig> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AiConfig config, CancellationToken ct = default);
}
