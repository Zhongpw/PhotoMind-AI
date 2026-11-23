using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
await using var provider = services.BuildServiceProvider();

provider.GetRequiredService<IAppPaths>().EnsureCreated();

var dbFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(dbFactory);

var repo = provider.GetRequiredService<IClassificationRepository>();
var failures = 0;

void Check(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");
    if (!ok) failures++;
}

// ---- 1. Seed ----
var categories = await repo.GetCategoriesAsync();
var tops = categories.Where(c => c.ParentId == null).OrderBy(c => c.SortOrder).ToList();
Check($"seed 6 top-level categories (got {tops.Count})", tops.Count == 6);
Console.WriteLine("  top-level: " + string.Join(", ", tops.Select(c => c.Name)));
var ziRan = tops.First(t => t.Name == "自然");
var haiYang = categories.First(c => c.Name == "海洋" && c.ParentId == ziRan.Id);
Check("自然 -> 海洋 child exists", haiYang.Id > 0);

// ---- 2. Insert test image + result ----
long imageId;
await using (var db = await dbFactory.CreateDbContextAsync())
{
    var img = new Image
    {
        FileName = "review-test.jpg",
        OriginalPath = @"D:\AI_Agent+\dsh_next\PhotoMindAI\TestData\sample\photo1.jpg",
        FileSize = 123,
        FileHash = "abc123def456",
        Width = 160,
        Height = 120,
        Format = ImageFormat.Jpeg,
        CreatedAt = DateTime.UtcNow,
        ModifiedAt = DateTime.UtcNow,
        ScanStatus = ImageScanStatus.Scanned,
        CreatedTime = DateTime.UtcNow,
    };
    db.Images.Add(img);
    await db.SaveChangesAsync();
    imageId = img.Id;
}

var result = new ClassificationResult
{
    ImageId = imageId,
    CategoryId = ziRan.Id,
    SubCategoryId = haiYang.Id,
    Description = "海边测试图",
    Reason = "画面主体是海洋",
    Confidence = 0.9,
    TagsJson = "[\"海洋\",\"沙滩\"]",
    NeedsReview = false,
    ModelName = "deepseek-flash",
    PromptVersion = "v1",
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow,
};
await repo.SaveResultAsync(result);

// ---- 3. Filtering ----
var all = await repo.GetResultSummariesAsync(new ReviewFilter(), 0, 100);
Check($"GetResultSummaries(all) count=1 (got {all.Count})", all.Count == 1);
var s = all[0];
Check("summary ResultId set", s.ResultId == result.Id);
Check("summary CategoryName=自然", s.CategoryName == "自然");
Check("summary SubCategoryName=海洋", s.SubCategoryName == "海洋");

var byCat = await repo.GetResultSummariesAsync(new ReviewFilter { CategoryId = ziRan.Id }, 0, 100);
Check("filter by 自然 -> 1", byCat.Count == 1);
var byOther = await repo.GetResultSummariesAsync(new ReviewFilter { CategoryId = tops.First(t => t.Name == "生活").Id }, 0, 100);
Check("filter by 生活 -> 0", byOther.Count == 0);
var bySub = await repo.GetResultSummariesAsync(new ReviewFilter { SubCategoryId = haiYang.Id }, 0, 100);
Check("filter by subcategory 海洋 -> 1", bySub.Count == 1);
var byConf = await repo.GetResultSummariesAsync(new ReviewFilter { MinConfidence = 0.95 }, 0, 100);
Check("filter confidence>=0.95 -> 0", byConf.Count == 0);
var bySearch = await repo.GetResultSummariesAsync(new ReviewFilter { Search = "沙滩" }, 0, 100);
Check("search '沙滩' (tags) -> 1", bySearch.Count == 1);
var byFile = await repo.GetResultSummariesAsync(new ReviewFilter { Search = "review-test" }, 0, 100);
Check("search 'review-test' (filename) -> 1", byFile.Count == 1);
var count = await repo.GetResultSummariesCountAsync(new ReviewFilter());
Check($"GetResultSummariesCount = 1 (got {count})", count == 1);

// ---- 4. Update result (user modifies category in review page) ----
var fetched = await repo.GetResultAsync(result.Id);
Check("GetResultAsync returns entity", fetched is not null);
var dongWu = tops.First(t => t.Name == "动物");
var mao = categories.First(c => c.Name == "猫" && c.ParentId == dongWu.Id);
fetched!.CategoryId = dongWu.Id;
fetched.SubCategoryId = mao.Id;
await repo.UpdateResultAsync(fetched);

var after = (await repo.GetResultSummariesAsync(new ReviewFilter(), 0, 100))[0];
Check("after update CategoryName=动物", after.CategoryName == "动物");
Check("after update SubCategoryName=猫", after.SubCategoryName == "猫");

// ---- 5. Category CRUD ----
var newCat = new Category
{
    Name = "测试分类",
    ParentId = null,
    Description = "临时",
    IsSystem = false,
    SortOrder = 99,
    CreatedAt = DateTime.UtcNow,
};
await repo.AddCategoryAsync(newCat);
var allCats = await repo.GetCategoriesAsync();
Check("add top-level category", allCats.Any(c => c.Name == "测试分类"));

newCat.Name = "测试分类改名";
await repo.UpdateCategoryAsync(newCat);
allCats = await repo.GetCategoriesAsync();
Check("rename category", allCats.Any(c => c.Name == "测试分类改名") && !allCats.Any(c => c.Name == "测试分类"));

await repo.DeleteCategoryAsync(newCat.Id);
allCats = await repo.GetCategoriesAsync();
Check("delete category", !allCats.Any(c => c.Id == newCat.Id));

var children = await repo.GetChildCategoriesAsync(ziRan.Id);
Check($"GetChildCategories(自然) = 5 (got {children.Count})", children.Count == 5);

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
return failures == 0 ? 0 : 1;
