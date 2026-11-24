using System.Text;
using System.Text.Json;
using PhotoMind.Core.Constants;

namespace PhotoMind.Infrastructure.AI;

/// <summary>
/// Byte-level BPE tokenizer for CLIP (ViT-B/32), faithful to the HuggingFace CLIPTokenizer:
/// lowercase → UTF-8 bytes → byte-to-unicode mapping → greedy BPE merges → vocab ids,
/// wrapped with &lt;|startoftext|&gt; / &lt;|endoftext|&gt; and padded to 77.
/// </summary>
public sealed class ClipTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<(string Left, string Right), int> _merges; // merge pair -> rank
    private static readonly Dictionary<byte, char> ByteToUnicode = BuildByteToUnicode();

    private ClipTokenizer(Dictionary<string, int> vocab, Dictionary<(string, string), int> merges)
    {
        _vocab = vocab;
        _merges = merges;
    }

    /// <summary>Loads the tokenizer from the CLIP vocab.json and merges.txt files.</summary>
    public static async Task<ClipTokenizer> LoadAsync(string vocabPath, string mergesPath, CancellationToken ct = default)
    {
        var vocabJson = await File.ReadAllTextAsync(vocabPath, ct).ConfigureAwait(false);
        var vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson)
                    ?? throw new InvalidOperationException("CLIP vocab.json is empty or invalid.");

        var mergesText = await File.ReadAllTextAsync(mergesPath, ct).ConfigureAwait(false);
        var merges = new Dictionary<(string, string), int>();
        var lines = mergesText.Split('\n');
        var rank = 0;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            var parts = line.Split(' ');
            if (parts.Length == 2)
                merges[(parts[0], parts[1])] = rank++;
        }

        return new ClipTokenizer(vocab, merges);
    }

    /// <summary>Tokenizes text into a fixed-length int array of <see cref="AiConstants.ClipTextSequenceLength"/>.</summary>
    public int[] Tokenize(string text)
    {
        text = text.ToLowerInvariant();

        // UTF-8 bytes → byte-to-unicode characters.
        var bytes = Encoding.UTF8.GetBytes(text);
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
            chars[i] = ByteToUnicode[bytes[i]];

        var mapped = new string(chars);
        const char spaceChar = '\u0120'; // 'Ġ' — byte 0x20 (space) maps here.

        var tokens = new List<int>(AiConstants.ClipTextSequenceLength) { AiConstants.ClipStartToken };

        var words = mapped.Split(spaceChar);
        for (var w = 0; w < words.Length; w++)
        {
            var word = (w > 0 ? spaceChar.ToString() : string.Empty) + words[w];
            if (word.Length == 0)
                continue;

            // Greedy BPE over the word's characters.
            var symbols = new List<string>(word.Length);
            foreach (var c in word)
                symbols.Add(c.ToString());

            while (true)
            {
                var bestRank = int.MaxValue;
                var bestIdx = -1;
                for (var i = 0; i < symbols.Count - 1; i++)
                {
                    if (_merges.TryGetValue((symbols[i], symbols[i + 1]), out var rank) && rank < bestRank)
                    {
                        bestRank = rank;
                        bestIdx = i;
                    }
                }

                if (bestIdx < 0)
                    break;

                symbols[bestIdx] = symbols[bestIdx] + symbols[bestIdx + 1];
                symbols.RemoveAt(bestIdx + 1);
            }

            foreach (var sym in symbols)
            {
                if (_vocab.TryGetValue(sym, out var id))
                    tokens.Add(id);
                // Unknown symbols are dropped (CLIP has no <unk>); they are rare for category labels.
            }
        }

        tokens.Add(AiConstants.ClipEndToken);

        if (tokens.Count > AiConstants.ClipTextSequenceLength)
            tokens.RemoveRange(AiConstants.ClipTextSequenceLength, tokens.Count - AiConstants.ClipTextSequenceLength);
        while (tokens.Count < AiConstants.ClipTextSequenceLength)
            tokens.Add(0);

        return tokens.ToArray();
    }

    /// <summary>The GPT-2/CLIP byte-to-unicode mapping (space → 'Ġ', etc.).</summary>
    private static Dictionary<byte, char> BuildByteToUnicode()
    {
        var bs = new List<int>();
        for (var b = '!'; b <= '~'; b++) bs.Add(b);   // 33..126 printable ASCII
        for (var b = 0xA1; b <= 0xAC; b++) bs.Add(b); // 161..172
        for (var b = 0xAE; b <= 0xFF; b++) bs.Add(b); // 174..255

        var cs = new List<int>(bs);
        var n = 0;
        for (var b = 0; b < 256; b++)
        {
            if (!bs.Contains(b))
            {
                bs.Add(b);
                cs.Add(256 + n);
                n++;
            }
        }

        var map = new Dictionary<byte, char>(256);
        for (var i = 0; i < bs.Count; i++)
            map[(byte)bs[i]] = (char)cs[i];
        return map;
    }
}
