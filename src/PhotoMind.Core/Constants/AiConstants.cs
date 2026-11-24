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

    // ---- Local CLIP (token-saving) settings ----

    /// <summary>Default CLIP softmax temperature.</summary>
    public const double DefaultClipTemperature = 0.07;

    /// <summary>Default CLIP softmax confidence threshold for accepting a result without DeepSeek.</summary>
    public const double DefaultClipConfidenceThreshold = 0.8;

    /// <summary>CLIP input image size (ViT-B/32 uses 224x224).</summary>
    public const int ClipImageSize = 224;

    /// <summary>CLIP embedding dimension (ViT-B/32 outputs 512).</summary>
    public const int ClipEmbeddingDim = 512;

    /// <summary>CLIP text sequence length (77 tokens, padded).</summary>
    public const int ClipTextSequenceLength = 77;

    /// <summary>CLIP tokenizer: <c>&lt;|startoftext|&gt;</c> token id.</summary>
    public const int ClipStartToken = 49406;

    /// <summary>CLIP tokenizer: <c>&lt;|endoftext|&gt;</c> token id.</summary>
    public const int ClipEndToken = 49407;

    /// <summary>CLIP tokenizer vocabulary size (49,408 byte-level BPE tokens).</summary>
    public const int ClipVocabSize = 49408;

    /// <summary>Per-channel mean used to normalize CLIP image input (RGB).</summary>
    public static readonly float[] ClipMean = { 0.48145466f, 0.4578275f, 0.40821073f };

    /// <summary>Per-channel standard deviation used to normalize CLIP image input (RGB).</summary>
    public static readonly float[] ClipStd = { 0.26862954f, 0.26130258f, 0.27577711f };
}
