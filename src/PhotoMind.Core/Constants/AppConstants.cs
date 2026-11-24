namespace PhotoMind.Core.Constants;

/// <summary>Application-wide constant values.</summary>
public static class AppConstants
{
    public const string AppName = "PhotoMind AI";
    public const string AppDisplayName = "PhotoMind AI";

    /// <summary>Environment variable that overrides the local data root (used by tests/sandbox).</summary>
    public const string DataDirEnvironmentVariable = "PHOTOMIND_DATA_DIR";

    public const string DatabaseFileName = "photomin.db";
    public const string LogFileName = "photomin-.log";
    public const string ThumbnailCacheDirectoryName = "thumbnails";
    public const string LogsDirectoryName = "logs";
    public const string TempDirectoryName = "temp";

    /// <summary>Non-secret AI settings file (stored in the data directory).</summary>
    public const string ConfigFileName = "ai-config.json";

    /// <summary>DPAPI-protected secret blob (API key).</summary>
    public const string SecretsFileName = "secrets.dat";

    /// <summary>Directory holding local model files (CLIP ONNX + tokenizer).</summary>
    public const string ModelsDirectoryName = "models";

    /// <summary>Sub-directory under models holding the CLIP ViT-B/32 files.</summary>
    public const string ClipModelDirectoryName = "clip";

    /// <summary>JSON file caching CLIP text embeddings (invalidated when the category tree changes).</summary>
    public const string ClipTextCacheFileName = "clip-text-cache.json";
}
