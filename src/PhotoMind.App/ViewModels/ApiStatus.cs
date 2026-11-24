using PhotoMind.Core.Constants;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.App.ViewModels;

/// <summary>
/// Top-bar API connection state. Distinguishes "saved" from "readable/decrypted" from "verified by a real call".
/// </summary>
public enum ApiStatusKind
{
    /// <summary>No valid key saved.</summary>
    NotConfigured = 0,

    /// <summary>Key is saved and decrypts successfully, but connectivity has not been verified.</summary>
    Configured = 1,

    /// <summary>Connectivity test succeeded.</summary>
    Connected = 2,

    /// <summary>A key exists but the connectivity test failed.</summary>
    Failed = 3,

    /// <summary>A key entry exists but it cannot be decrypted (corrupt or saved under another Windows user).</summary>
    Error = 4,
}

/// <summary>Broadcast when the top-bar API status should change.</summary>
public sealed record ApiStatusChangedMessage(ApiStatusKind Kind);

/// <summary>Maps the secret store into a top-bar API status and a display text.</summary>
public static class ApiStatusDetector
{
    /// <summary>
    /// Determines the API status from the secret store. Returns <see cref="ApiStatusKind.Error"/> when a key entry
    /// exists but cannot be decrypted, and <see cref="ApiStatusKind.NotConfigured"/> when nothing is stored.
    /// </summary>
    public static async Task<ApiStatusKind> DetectAsync(ISecretStore store, CancellationToken ct = default)
    {
        if (!await store.HasSecretAsync(AiConstants.ApiKeySecretName, ct).ConfigureAwait(false))
            return ApiStatusKind.NotConfigured;

        var value = await store.GetSecretAsync(AiConstants.ApiKeySecretName, ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? ApiStatusKind.Error : ApiStatusKind.Configured;
    }

    /// <summary>Display text for the top-bar status.</summary>
    public static string ToText(ApiStatusKind kind) => kind switch
    {
        ApiStatusKind.NotConfigured => "未配置",
        ApiStatusKind.Configured => "已配置",
        ApiStatusKind.Connected => "已连接",
        ApiStatusKind.Failed => "连接失败",
        ApiStatusKind.Error => "密钥异常",
        _ => "未知",
    };
}
