namespace PhotoMind.Core.Interfaces;

/// <summary>Resolves local data-directory locations (DB, thumbnails, logs).</summary>
public interface IAppPaths
{
    string DataDirectory { get; }
    string DatabasePath { get; }
    string ThumbnailDirectory { get; }
    string LogDirectory { get; }
    string TempDirectory { get; }
    string ConfigFilePath { get; }
    string SecretsFilePath { get; }

    /// <summary>Root directory for local model files (CLIP ONNX, tokenizer).</summary>
    string ModelsDirectory { get; }

    /// <summary>Directory holding the CLIP ViT-B/32 ONNX + tokenizer files.</summary>
    string ClipModelDirectory { get; }

    /// <summary>Path of the CLIP text-embedding cache file.</summary>
    string ClipTextCacheFilePath { get; }

    /// <summary>Creates the data, thumbnail, log and temp directories if they do not yet exist.</summary>
    void EnsureCreated();
}
