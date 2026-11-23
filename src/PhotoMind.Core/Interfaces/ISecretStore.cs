namespace PhotoMind.Core.Interfaces;

/// <summary>Encrypted storage for small secrets (currently the DeepSeek API key).</summary>
public interface ISecretStore
{
    Task SaveSecretAsync(string key, string value, CancellationToken ct = default);
    Task<string?> GetSecretAsync(string key, CancellationToken ct = default);
    Task DeleteSecretAsync(string key, CancellationToken ct = default);
    Task<bool> HasSecretAsync(string key, CancellationToken ct = default);
}
