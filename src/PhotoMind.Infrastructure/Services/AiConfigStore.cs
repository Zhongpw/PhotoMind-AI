using System.Text.Json;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.Services;

/// <summary>Loads/saves <see cref="AiConfig"/> as JSON. Only non-secret settings live here.</summary>
public sealed class AiConfigStore : IAiConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public AiConfigStore(IAppPaths paths) => _filePath = paths.ConfigFilePath;

    public async Task<AiConfig> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return new AiConfig();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
            var config = JsonSerializer.Deserialize<AiConfig>(json);
            return config?.Sanitized() ?? new AiConfig();
        }
        catch (JsonException)
        {
            return new AiConfig();
        }
    }

    public async Task SaveAsync(AiConfig config, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(config.Sanitized(), JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct).ConfigureAwait(false);
    }
}
