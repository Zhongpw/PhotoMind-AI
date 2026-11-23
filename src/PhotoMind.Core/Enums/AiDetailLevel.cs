namespace PhotoMind.Core.Enums;

/// <summary>Vision detail level passed to the DeepSeek/OpenAI-compatible API.</summary>
public enum AiDetailLevel
{
    /// <summary>Reduced detail for normal photos (default; lower token usage).</summary>
    Low = 0,

    /// <summary>Full detail for fine-grained recognition (higher token usage).</summary>
    Original = 1,
}
