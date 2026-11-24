using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhotoMind.Core.Models;

namespace PhotoMind.Infrastructure.Database;

/// <summary>Seeds the built-in category tree on first run (only when the Categories table is empty).</summary>
public static class CategorySeed
{
    private sealed record SeedCategory(
        string Name,
        string Alias,
        string Description,
        string[] Prompts,
        (string Name, string Alias, string[] Prompts)[] Subs);

    // English aliases feed the CLIP text encoder. Each category carries a small ensemble of natural,
    // specific noun phrases; CLIP embeds each under "a photo of {prompt}" and averages them.
    private static readonly SeedCategory[] TopLevel =
    {
        new("自然", "nature", "自然风光与户外景色", new[]
        {
            "a natural landscape",
            "outdoor scenery",
            "mountains forests oceans or sky",
        }, new[]
        {
            ("海洋", "the ocean", new[] { "the ocean", "sea and waves", "a beach with water" }),
            ("山川", "mountains", new[] { "mountains", "a mountain landscape", "rocky peaks" }),
            ("花草", "flowers", new[] { "flowers", "blooming plants", "a flower garden" }),
            ("森林", "forest", new[] { "a forest", "dense trees and woods", "woodland" }),
            ("天空", "the sky", new[] { "the sky", "clouds in the sky", "a sunset or blue sky" }),
        }),
        new("生活", "everyday life", "日常生活记录", new[]
        {
            "people in everyday life",
            "daily life activities",
            "an indoor or social human scene",
        }, new[]
        {
            ("日常", "daily routine", new[] { "daily routine activities", "everyday chores", "a person doing daily tasks" }),
            ("聚会", "a party", new[] { "a party", "people celebrating together", "a social gathering" }),
            ("家庭", "family", new[] { "a family", "family members together", "family life at home" }),
            ("旅行", "travel", new[] { "travel", "a journey or trip", "sightseeing while traveling" }),
        }),
        new("动物", "animals", "动物与宠物", new[]
        {
            "an animal",
            "a wild or domestic animal",
            "wildlife",
        }, new[]
        {
            ("猫", "a cat", new[] { "a cat", "a feline pet", "a kitten or cat" }),
            ("狗", "a dog", new[] { "a dog", "a canine pet", "a puppy or dog" }),
            ("鸟", "a bird", new[] { "a bird", "a flying bird", "birds" }),
            ("其他动物", "other animals", new[] { "other animals", "wild animals", "an animal in the wild" }),
        }),
        new("建筑", "architecture", "建筑与室内", new[]
        {
            "a building",
            "architecture and urban structures",
            "a man-made structure",
        }, new[]
        {
            ("城市", "a city", new[] { "a city", "urban streets and buildings", "a cityscape" }),
            ("室内", "an indoor room", new[] { "an indoor room", "the interior of a building", "a room inside" }),
            ("地标", "a landmark", new[] { "a famous landmark", "a monument", "an iconic structure" }),
        }),
        new("美食", "food", "食物与饮品", new[]
        {
            "food on a plate",
            "a dish or meal",
            "cooked food and drinks",
        }, new[]
        {
            ("中餐", "Chinese food", new[] { "Chinese food", "Chinese cuisine", "a Chinese dish" }),
            ("西餐", "Western food", new[] { "Western food", "Western cuisine", "a western dish" }),
            ("甜点", "dessert", new[] { "dessert", "a sweet dessert", "cake or pastry" }),
            ("饮品", "drinks", new[] { "drinks", "a beverage", "coffee tea or juice" }),
        }),
        new("其他", "miscellaneous", "无法归入以上分类的内容", new[]
        {
            "a miscellaneous object",
            "other unclassified things",
        }, Array.Empty<(string Name, string Alias, string[] Prompts)>()),
    };

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Categories.AnyAsync(ct).ConfigureAwait(false))
            return;

        var now = DateTime.UtcNow;
        var parents = new List<Category>(TopLevel.Length);
        for (var i = 0; i < TopLevel.Length; i++)
        {
            parents.Add(new Category
            {
                Name = TopLevel[i].Name,
                Alias = TopLevel[i].Alias,
                PromptsJson = JsonSerializer.Serialize(TopLevel[i].Prompts),
                Description = TopLevel[i].Description,
                IsSystem = true,
                SortOrder = i,
                CreatedAt = now,
            });
        }

        db.Categories.AddRange(parents);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var children = new List<Category>();
        for (var i = 0; i < TopLevel.Length; i++)
        {
            var subs = TopLevel[i].Subs;
            for (var j = 0; j < subs.Length; j++)
            {
                children.Add(new Category
                {
                    Name = subs[j].Name,
                    Alias = subs[j].Alias,
                    PromptsJson = JsonSerializer.Serialize(subs[j].Prompts),
                    ParentId = parents[i].Id,
                    IsSystem = true,
                    SortOrder = j,
                    CreatedAt = now,
                });
            }
        }

        db.Categories.AddRange(children);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
