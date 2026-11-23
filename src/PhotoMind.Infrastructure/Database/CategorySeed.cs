using Microsoft.EntityFrameworkCore;
using PhotoMind.Core.Models;

namespace PhotoMind.Infrastructure.Database;

/// <summary>Seeds the built-in category tree on first run (only when the Categories table is empty).</summary>
public static class CategorySeed
{
    private static readonly (string Name, string Description, string[] Subs)[] TopLevel =
    {
        ("自然", "自然风光与户外景色", new[] { "海洋", "山川", "花草", "森林", "天空" }),
        ("生活", "日常生活记录", new[] { "日常", "聚会", "家庭", "旅行" }),
        ("动物", "动物与宠物", new[] { "猫", "狗", "鸟", "其他动物" }),
        ("建筑", "建筑与室内", new[] { "城市", "室内", "地标" }),
        ("美食", "食物与饮品", new[] { "中餐", "西餐", "甜点", "饮品" }),
        ("其他", "无法归入以上分类的内容", Array.Empty<string>()),
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
                    Name = subs[j],
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
