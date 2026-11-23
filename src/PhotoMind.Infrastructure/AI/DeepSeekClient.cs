using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.Infrastructure.AI;

/// <summary>Stateless HTTP client for the DeepSeek OpenAI-compatible /chat/completions endpoint.</summary>
public sealed class DeepSeekClient : IDeepSeekClient
{
    // Bypass the system proxy by default: a configured WinINet proxy (e.g. 127.0.0.1:7890)
    // can be down or unreachable and would otherwise break direct access to api.deepseek.com.
    private static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false })
    {
        Timeout = TimeSpan.FromSeconds(120)
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DeepSeekResponse> ChatAsync(string baseUrl, string apiKey, DeepSeekRequest request, CancellationToken ct = default)
    {
        var url = baseUrl.TrimEnd('/') + AiConstants.ChatCompletionsPath;

        string body;
        HttpStatusCode status;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");

            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            status = resp.StatusCode;
            body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new DeepSeekException(DeepSeekErrorKind.Network, "网络连接失败，请检查网络后重试。", inner: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new DeepSeekException(DeepSeekErrorKind.Network, "请求超时，请稍后重试。", inner: ex);
        }

        if (status == HttpStatusCode.OK)
        {
            var parsed = JsonSerializer.Deserialize<DeepSeekResponse>(body, JsonOptions);
            return parsed ?? throw new DeepSeekException(DeepSeekErrorKind.InvalidResponse, "API 返回了空响应。");
        }

        DeepSeekErrorBody? error = null;
        try
        {
            error = JsonSerializer.Deserialize<DeepSeekResponse>(body, JsonOptions)?.Error;
        }
        catch (JsonException)
        {
            // Non-JSON error body; fall back to the raw text.
        }

        throw MapError(status, error?.Message ?? body);
    }

    private static DeepSeekException MapError(HttpStatusCode status, string message)
    {
        var code = (int)status;
        var lowered = message.ToLowerInvariant();

        if (lowered.Contains("balance") || lowered.Contains("insufficient") || message.Contains("余额") || message.Contains("资金"))
            return new DeepSeekException(DeepSeekErrorKind.InsufficientBalance, "API 余额不足，请充值后重试。", code);

        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new DeepSeekException(DeepSeekErrorKind.Authentication, "API Key 无效或已过期，请检查设置。", code),
            HttpStatusCode.TooManyRequests =>
                new DeepSeekException(DeepSeekErrorKind.RateLimit, "请求过于频繁，已被限流，将自动重试。", code),
            HttpStatusCode.PaymentRequired =>
                new DeepSeekException(DeepSeekErrorKind.InsufficientBalance, "API 余额不足，请充值后重试。", code),
            _ when code >= 500 =>
                new DeepSeekException(DeepSeekErrorKind.Server, "DeepSeek 服务器暂时不可用，将自动重试。", code),
            _ =>
                new DeepSeekException(DeepSeekErrorKind.Request, message, code),
        };
    }
}
