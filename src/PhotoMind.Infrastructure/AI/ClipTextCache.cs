using System.Runtime.InteropServices;
using System.Text.Json;

namespace PhotoMind.Infrastructure.AI;

/// <summary>
/// Persistent cache of CLIP text embeddings, keyed by "categoryId|label". A rename/alias change
/// produces a new key (so the stale entry is simply bypassed); additions produce new keys; deletions
/// leave orphaned entries that are harmless. Kept separate from every other cache and stored as a
/// single JSON file in the data directory.
/// </summary>
public sealed class ClipTextCache
{
    private readonly string _filePath;
    private readonly Dictionary<string, float[]> _entries = new();

    public ClipTextCache(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public static string MakeKey(long categoryId, string label) => $"{categoryId}|{label}";

    public bool TryGet(string key, out float[]? embedding) => _entries.TryGetValue(key, out embedding);

    public void Set(string key, float[] embedding) => _entries[key] = embedding;

    public int Count => _entries.Count;

    public void Save()
    {
        var serializable = new Dictionary<string, string>(_entries.Count);
        foreach (var (key, embedding) in _entries)
            serializable[key] = Encode(embedding);

        var json = JsonSerializer.Serialize(new { version = 1, entries = serializable });
        File.WriteAllText(_filePath, json);
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_filePath));
            if (doc.RootElement.TryGetProperty("entries", out var entriesEl))
            {
                foreach (var prop in entriesEl.EnumerateObject())
                {
                    var embedding = Decode(prop.Value.GetString());
                    if (embedding is not null)
                        _entries[prop.Name] = embedding;
                }
            }
        }
        catch
        {
            // Corrupt cache is non-fatal: start empty and rebuild on demand.
            _entries.Clear();
        }
    }

    private static string Encode(float[] values)
    {
        var bytes = MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        return Convert.ToBase64String(bytes);
    }

    private static float[]? Decode(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        catch
        {
            return null;
        }
    }
}
