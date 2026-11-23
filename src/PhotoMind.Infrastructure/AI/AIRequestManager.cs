using PhotoMind.Core.DTOs;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;
using Serilog;

namespace PhotoMind.Infrastructure.AI;

/// <summary>Sends one request with retry + exponential backoff for transient failures (network / 429 / 5xx).</summary>
public sealed class AIRequestManager
{
    private readonly IDeepSeekClient _client;
    private readonly ILogger _logger;

    public AIRequestManager(IDeepSeekClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<DeepSeekResponse> SendWithRetryAsync(
        string baseUrl,
        string apiKey,
        DeepSeekRequest request,
        int maxRetries,
        CancellationToken ct = default)
    {
        Exception? last = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await _client.ChatAsync(baseUrl, apiKey, request, ct).ConfigureAwait(false);
            }
            catch (DeepSeekException ex) when (ex.IsRetryable && attempt < maxRetries)
            {
                last = ex;
                _logger.Warning("DeepSeek 请求失败（第 {Attempt}/{Max} 次）：{Kind} {Message}", attempt + 1, maxRetries + 1, ex.Kind, ex.Message);
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        throw new DeepSeekException(DeepSeekErrorKind.Request, $"请求重试 {maxRetries} 次后仍然失败。", inner: last);
    }
}
