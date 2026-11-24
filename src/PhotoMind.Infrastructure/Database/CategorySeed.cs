using Microsoft.EntityFrameworkCore;
using PhotoMind.Core.Models;

namespace PhotoMind.Infrastructure.Database;

/// <summary>Seeds the built-in category tree on first run (only when the Categories table is empty).</summary>
public static class CategorySeed
{
    private sealed record SeedCategory(string Name, string Alias, string Description, (string Name, string Alias)[] Subs);

    // English aliases feed the CLIP text encoder, which understands English far better than Chinese.
    private static readonly SeedCategory[] TopLevel =
    {
        new("自然", "nature", "自然风光与户外景色", new[]
        {
            ("海洋", "the ocean"),
            ("山川", "mountains"),
            ("花草", "flowers"),
            ("森林", "forest"),
            ("天空", "the sky"),
        }),
        new("生活", "everyday life", "日常生活记录", new[]
        {
            ("日常", "daily routine"),
            ("聚会", "a party"),
            ("家庭", "family"),
            ("旅行", "travel"),
        }),
        new("动物", "animals", "动物与宠物", new[]
        {
            ("猫", "a cat"),
            ("狗", "a dog"),
            ("鸟", "a bird"),
            ("其他动物", "other animals"),
        }),
        new("建筑", "architecture", "建筑与室内", new[]
        {
            ("城市", "a city"),
            ("室内", "an indoor room"),
            ("地标", "a landmark"),
        }),
        new("美食", "food", "食物与饮品", new[]
        {
            ("中餐", "Chinese food"),
            ("西餐", "Western food"),
            ("甜点", "dessert"),
            ("饮品", "drinks"),
        }),
        new("其他", "miscellaneous", "无法归入以上分类的内容", Array.Empty<(string Name, string Alias)>()),
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
