using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Models;
using PhotoMind.Infrastructure.AI;

namespace PhotoMind.Tests.AI;

/// <summary>Verifies that the CLIP candidate-screening hint is injected into the DeepSeek system prompt
/// only when candidates are supplied, while the full category list always remains for fallback.</summary>
public sealed class PromptBuilderTests
{
    private static IReadOnlyList<Category> Categories() => new List<Category>
    {
        new() { Id = 1, Name = "动物", ParentId = null },
        new() { Id = 11, Name = "猫", ParentId = 1 },
    };

    [Fact]
    public void BuildMessages_WithCandidates_IncludesCandidateHint()
    {
        var candidates = new List<ClipCandidate>
        {
            new() { CategoryId = 1, Name = "动物", ParentId = null },
            new() { CategoryId = 11, Name = "猫", ParentId = 1, ParentName = "动物" },
        };

        var messages = PromptBuilder.BuildMessages("data:image/jpeg;base64,AAAA", ClassificationMode.Hybrid, Categories(), AiDetailLevel.Low, candidates);

        var system = (string)messages[0].Content!;
        Assert.Contains("本地图像模型已初步筛选出以下候选分类", system);
        Assert.Contains("1. 动物", system);
        Assert.Contains("2. 动物 > 猫", system);
        Assert.Contains("请优先从以上候选分类中选择", system);
        // Full list must still be present for fallback.
        Assert.Contains("当前已有的分类体系", system);
        Assert.Contains("- 动物", system);
    }

    [Fact]
    public void BuildMessages_WithoutCandidates_NoCandidateHint()
    {
        var messages = PromptBuilder.BuildMessages("data:image/jpeg;base64,AAAA", ClassificationMode.Hybrid, Categories(), AiDetailLevel.Low, null);

        var system = (string)messages[0].Content!;
        Assert.DoesNotContain("本地图像模型已初步筛选", system);
        Assert.DoesNotContain("候选分类中选择", system);
        Assert.Contains("当前已有的分类体系", system);
    }
}
