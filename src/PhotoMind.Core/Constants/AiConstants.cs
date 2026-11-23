namespace PhotoMind.Core.Constants;

/// <summary>Defaults for the DeepSeek integration (kept separate from secrets).</summary>
public static class AiConstants
{
    public const string DefaultBaseUrl = "https://api.deepseek.com";
    public const string DefaultModel = "deepseek-flash";
    public const string ChatCompletionsPath = "/chat/completions";

    /// <summary>Initial concurrent request count (user-adjustable 1..5).</summary>
    public const int DefaultConcurrency = 3;
    public const int MinConcurrency = 1;
    public const int MaxConcurrency = 5;

    /// <summary>Default max retries per image.</summary>
    public const int DefaultMaxRetries = 3;

    /// <summary>Default maximum edge of the AI preview image in pixels.</summary>
    public const int DefaultMaxPreviewDimension = 1024;

    /// <summary>Default JPEG quality (1..100) for the generated AI preview.</summary>
    public const int DefaultPreviewQuality = 85;

    /// <summary>Secret-store key under which the DeepSeek API key is persisted.</summary>
    public const string ApiKeySecretName = "deepseek-api-key";
}
