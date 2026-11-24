using PhotoMind.Core.Exceptions;
using PhotoMind.Infrastructure.AI;

namespace PhotoMind.Tests.AI;

public class AIResponseParserTests
{
    [Fact]
    public void Parse_ValidJson_ExtractsAllFields()
    {
        const string json = """
        {
            "category": "自然",
            "subcategory": "海洋",
            "tags": ["海", "水", "蓝"],
            "description": "一片海景",
            "reason": "有海和天空",
            "confidence": 0.93,
            "needs_review": false,
            "suggested_category": null
        }
        """;

        var result = AIResponseParser.Parse(json);

        Assert.Equal("自然", result.Category);
        Assert.Equal("海洋", result.Subcategory);
        Assert.Equal(new[] { "海", "水", "蓝" }, result.Tags);
        Assert.Equal("一片海景", result.Description);
        Assert.Equal(0.93, result.Confidence, 3);
        Assert.False(result.NeedsReview);
    }

    [Fact]
    public void Parse_MarkdownFence_StripsFence()
    {
        const string content = """
        ```json
        {"category": "建筑", "confidence": 0.8}
        ```
        """;

        var result = AIResponseParser.Parse(content);

        Assert.Equal("建筑", result.Category);
        Assert.Equal(0.8, result.Confidence, 3);
    }

    [Fact]
    public void Parse_ProseAroundJson_ExtractsSpan()
    {
        const string content = "识别完成，结果是 {\"category\": \"动物\", \"confidence\": 0.7}，谢谢";

        var result = AIResponseParser.Parse(content);

        Assert.Equal("动物", result.Category);
    }

    [Fact]
    public void Parse_ConfidenceAboveOne_ClampedToOne()
    {
        var result = AIResponseParser.Parse("{\"category\": \"美食\", \"confidence\": 1.5}");
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public void Parse_ConfidenceBelowZero_ClampedToZero()
    {
        var result = AIResponseParser.Parse("{\"category\": \"美食\", \"confidence\": -0.2}");
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void Parse_LowConfidence_ForcesReview()
    {
        var result = AIResponseParser.Parse("{\"category\": \"生活\", \"confidence\": 0.4, \"needs_review\": false}");
        Assert.True(result.NeedsReview);
    }

    [Fact]
    public void Parse_HighConfidence_KeepsReviewFalse()
    {
        var result = AIResponseParser.Parse("{\"category\": \"生活\", \"confidence\": 0.9, \"needs_review\": false}");
        Assert.False(result.NeedsReview);
    }

    [Fact]
    public void Parse_InvalidNameCharacters_SanitizedToSpace()
    {
        var result = AIResponseParser.Parse("{\"category\": \"自然/海洋:风景\", \"confidence\": 0.9}");
        Assert.Equal("自然 海洋 风景", result.Category);
    }

    [Fact]
    public void Parse_EmptyCategory_IsUnclassified()
    {
        var result = AIResponseParser.Parse("{\"category\": \"\", \"confidence\": 0.9}");
        Assert.True(result.IsUnclassified);
    }

    [Fact]
    public void Parse_MissingJson_ThrowsInvalidResponse()
    {
        var ex = Assert.Throws<DeepSeekException>(() => AIResponseParser.Parse("没有 JSON"));
        Assert.Equal(DeepSeekErrorKind.InvalidResponse, ex.Kind);
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsInvalidResponse()
    {
        var ex = Assert.Throws<DeepSeekException>(() => AIResponseParser.Parse("{not valid json"));
        Assert.Equal(DeepSeekErrorKind.InvalidResponse, ex.Kind);
    }

    [Fact]
    public void Parse_NonObjectJson_ThrowsInvalidResponse()
    {
        var ex = Assert.Throws<DeepSeekException>(() => AIResponseParser.Parse("[1,2,3]"));
        Assert.Equal(DeepSeekErrorKind.InvalidResponse, ex.Kind);
    }

    [Fact]
    public void Parse_NonStringTags_Ignored()
    {
        var result = AIResponseParser.Parse("{\"category\": \"x\", \"tags\": [\"a\", 1, true, \"b\"], \"confidence\": 0.9}");
        Assert.Equal(new[] { "a", "b" }, result.Tags);
    }
}
