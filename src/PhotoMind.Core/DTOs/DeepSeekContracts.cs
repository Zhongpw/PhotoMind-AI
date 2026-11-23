using System.Text.Json.Serialization;

namespace PhotoMind.Core.DTOs;

// Wire-format contracts for the DeepSeek OpenAI-compatible /chat/completions endpoint.

/// <summary>Outbound request body.</summary>
public sealed class DeepSeekRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<DeepSeekMessage> Messages { get; set; } = new();
}

/// <summary>Outbound message. <see cref="Content"/> is a string for the system role, or a list of parts for the user role.</summary>
public sealed class DeepSeekMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public object? Content { get; set; }
}

/// <summary>A vision content part (image_url block).</summary>
public sealed class DeepSeekContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "image_url";

    [JsonPropertyName("image_url")]
    public DeepSeekImageUrl ImageUrl { get; set; } = new();
}

/// <summary>The base64 data URL and detail level of one image.</summary>
public sealed class DeepSeekImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = "low";
}

/// <summary>Inbound response body.</summary>
public sealed class DeepSeekResponse
{
    [JsonPropertyName("choices")]
    public List<DeepSeekChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public DeepSeekUsage? Usage { get; set; }

    [JsonPropertyName("error")]
    public DeepSeekErrorBody? Error { get; set; }
}

public sealed class DeepSeekChoice
{
    [JsonPropertyName("message")]
    public DeepSeekResponseMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>Inbound assistant message; content is the plain-text JSON the model returns.</summary>
public sealed class DeepSeekResponseMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "assistant";

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class DeepSeekUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>Error payload returned with a non-2xx status (OpenAI-compatible).</summary>
public sealed class DeepSeekErrorBody
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
