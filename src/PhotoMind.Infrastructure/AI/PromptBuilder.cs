using System.Text;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Models;

namespace PhotoMind.Infrastructure.AI;

/// <summary>Builds the system prompt and vision message for a single image classification request.</summary>
public static class PromptBuilder
{
    public const string PromptVersion = "v1";

    private const string BaseSystemPrompt =
        """
        你是一名专业的照片内容识别和分类助手。
        请分析用户提供的图片，完成以下任务：
        1. 判断图片的主要视觉内容。
        2. 判断图片所属分类。
        3. 提取有价值的标签。
        4. 生成简洁的图片描述。
        5. 判断图片是否存在模糊、遮挡或无法识别的问题。
        6. 给出合理的分类置信度。

        分类原则：
        - 以实际视觉内容为准。
        - 不根据文件名进行无依据推断。
        - 不猜测照片中人物的真实身份。
        - 不推断人物敏感属性。
        - 不将不确定的信息当作事实。
        - 如果无法判断，返回"未分类"。

        只返回一个 JSON 对象，不要包含任何其他文字或 Markdown 代码块，结构如下：
        {"category":"自然","subcategory":"海洋","tags":["海洋","海滩","日落"],"description":"一张海边日落的照片","reason":"图片主体是海洋和日落景色","confidence":0.96,"needs_review":false,"suggested_category":null}
        """;

    public static List<DeepSeekMessage> BuildMessages(
        string base64DataUrl,
        ClassificationMode mode,
        IReadOnlyList<Category> categories,
        AiDetailLevel detail,
        IReadOnlyList<ClipCandidate>? candidates = null)
    {
        return new List<DeepSeekMessage>
        {
            new() { Role = "system", Content = BuildSystemPrompt(mode, categories, candidates) },
            new() { Role = "user", Content = BuildUserContent(base64DataUrl, detail) },
        };
    }

    private static string BuildSystemPrompt(
        ClassificationMode mode,
        IReadOnlyList<Category> categories,
        IReadOnlyList<ClipCandidate>? candidates)
    {
        var sb = new StringBuilder(BaseSystemPrompt);

        // Candidate screening: prepend a ranked hint, but keep DeepSeek free to observe independently
        // and to choose any category from the full list below (never forced into a wrong branch).
        if (candidates is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("本地图像模型已初步筛选出以下候选分类（按相似度降序，仅供参考，不要盲从）：");
            var rank = 0;
            foreach (var c in candidates)
            {
                rank++;
                var path = c.ParentName is null ? c.Name : $"{c.ParentName} > {c.Name}";
                sb.AppendLine($"{rank}. {path}");
            }
            sb.AppendLine("请优先从以上候选分类中选择，但必须独立观察图片后再判断；如果都不合适，可从下方完整分类体系中另选。");
        }

        var topLevel = categories.Where(c => c.ParentId == null).Select(c => c.Name).Distinct().ToList();
        if (topLevel.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("当前已有的分类体系：");
            foreach (var name in topLevel)
                sb.AppendLine($"- {name}");

            if (mode == ClassificationMode.Fixed)
            {
                sb.AppendLine("你必须只从以上分类中选择 category，不得创建新分类；如果都不匹配，返回\"未分类\"。");
            }
            else if (mode == ClassificationMode.Hybrid)
            {
                sb.AppendLine("请优先从以上分类中选择 category；如果确实都不匹配，可在 suggested_category 中提出一个新的顶级分类名称。");
            }
        }

        return sb.ToString();
    }

    private static List<DeepSeekContentPart> BuildUserContent(string base64DataUrl, AiDetailLevel detail)
    {
        return new List<DeepSeekContentPart>
        {
            new()
            {
                Type = "image_url",
                ImageUrl = new DeepSeekImageUrl
                {
                    Url = base64DataUrl,
                    Detail = detail == AiDetailLevel.Original ? "original" : "low",
                },
            },
        };
    }
}
