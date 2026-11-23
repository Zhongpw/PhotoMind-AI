using PhotoMind.Core.DTOs;

namespace PhotoMind.Core.Interfaces;

/// <summary>Low-level stateless client for the DeepSeek OpenAI-compatible /chat/completions endpoint.</summary>
public interface IDeepSeekClient
{
    Task<DeepSeekResponse> ChatAsync(string baseUrl, string apiKey, DeepSeekRequest request, CancellationToken ct = default);
}
