using System.Text.Json;
using System.Text.RegularExpressions;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Exceptions;

namespace PhotoMind.Infrastructure.AI;

/// <summary>
/// Parses and validates the AI's JSON output. Model output is untrusted: names are sanitized,
/// confidence is clamped, and malformed content raises a friendly error.
/// </summary>
public static class AIResponseParser
{
    /// <summary>Below this confidence a result is flagged for human review.</summary>
    public const double ReviewConfidenceThreshold = 0.6;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AiRecognitionResult Parse(string content)
    {
        var json = ExtractJson(content);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new DeepSeekException(DeepSeekErrorKind.InvalidResponse, "AI 返回的 JSON 无法解析。", inner: ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new DeepSeekException(DeepSeekErrorKind.InvalidResponse, "AI 返回的不是 JSON 对象。");

            var result = new AiRecognitionResult
            {
                Category = SanitizeName(GetString(root, "category")),
                Subcategory = NormalizeOptional(GetString(root, "subcategory")),
                Description = GetString(root, "description"),
                Reason = GetString(root, "reason"),
                Confidence = Math.Clamp(GetDouble(root, "confidence"), 0.0, 1.0),
                NeedsReview = GetBool(root, "needs_review"),
                SuggestedCategory = NormalizeOptional(GetString(root, "suggested_category")),
                Tags = GetTags(root),
            };

            if (result.Confidence < ReviewConfidenceThreshold)
                result.NeedsReview = true;

            return result;
        }
    }

    private static string ExtractJson(string content)
    {
        var text = content.Trim();

        // Strip a ```json ... ``` code fence if present.
        var fence = Regex.Match(text, @"```(?:json)?\s*(\{.*?\})\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (fence.Success)
            return fence.Groups[1].Value;

        // Fall back to the first '{' … last '}' span.
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        throw new DeepSeekException(DeepSeekErrorKind.InvalidResponse, "无法从 AI 响应中提取 JSON。");
    }

    private static string GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? string.Empty : string.Empty;

    private static bool GetBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False && prop.GetBoolean();

    private static double GetDouble(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d) ? d : 0.0;

    private static IReadOnlyList<string> GetTags(JsonElement obj)
    {
        if (!obj.TryGetProperty("tags", out var prop) || prop.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var tags = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var tag = SanitizeName(item.GetString() ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(tag))
                    tags.Add(tag);
            }
        }
        return tags;
    }

    private static string? NormalizeOptional(string value)
        => string.IsNullOrWhiteSpace(value) ? null : SanitizeName(value);

    /// <summary>Removes characters that are invalid in a Windows path and collapses whitespace.</summary>
    private static string SanitizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        var chars = value.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|')
                chars[i] = ' ';
        }
        return new string(chars).Trim();
    }
}
