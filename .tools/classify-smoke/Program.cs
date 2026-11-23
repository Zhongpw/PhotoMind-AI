using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.AI;
using PhotoMind.Infrastructure.Database;
using Serilog;

// Use an isolated data directory so this never touches real user data.
var dataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".testdata-classify-smoke");
dataDir = Path.GetFullPath(dataDir);
if (Directory.Exists(dataDir))
    Directory.Delete(dataDir, recursive: true);
Environment.SetEnvironmentVariable("PHOTOMIND_DATA_DIR", dataDir);

Log.Logger = new LoggerConfiguration().MinimumLevel.Information().CreateLogger();

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
await using var sp = services.BuildServiceProvider();

var paths = sp.GetRequiredService<IAppPaths>();
paths.EnsureCreated();
var dbFactory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(dbFactory);

var pass = 0;
var fail = 0;
void Check(bool ok, string name)
{
    if (ok) { pass++; Console.WriteLine($"PASS  {name}"); }
    else { fail++; Console.WriteLine($"FAIL  {name}"); }
}

// ---------- 1. AIResponseParser ----------
var r1 = AIResponseParser.Parse(
    """{"category":"自然","subcategory":"海洋","tags":["海洋","海滩"],"description":"海边","reason":"主体是海","confidence":0.96,"needs_review":false}""");
Check(r1.Category == "自然" && r1.Subcategory == "海洋" && r1.Tags.Count == 2 && r1.Confidence == 0.96 && !r1.NeedsReview,
    "parser: normal JSON");

var r2 = AIResponseParser.Parse("""
好的，分析结果如下：
```json
{"category":"美食","tags":["蛋糕"],"confidence":0.8,"needs_review":false}
```
""");
Check(r2.Category == "美食" && r2.Tags.Contains("蛋糕"), "parser: fenced JSON");

var r3 = AIResponseParser.Parse("""{"category":"动物","confidence":0.3,"needs_review":false}""");
Check(r3.NeedsReview, "parser: low confidence forces review");

var r4 = AIResponseParser.Parse("""{"category":"建筑","confidence":1.7,"needs_review":false}""");
Check(r4.Confidence == 1.0, "parser: confidence clamped to [0,1]");

var r5 = AIResponseParser.Parse("""{"category":"自然:海洋/山川","confidence":0.9,"needs_review":false}""");
Check(r5.Category == "自然 海洋 山川", "parser: path chars sanitized to spaces");

var r6 = AIResponseParser.Parse("""{"category":"未分类","confidence":0.9,"needs_review":false}""");
Check(r6.IsUnclassified, "parser: 未分类 -> IsUnclassified");

try { AIResponseParser.Parse("this is not json"); Check(false, "parser: invalid JSON throws"); }
catch (DeepSeekException) { Check(true, "parser: invalid JSON throws DeepSeekException"); }

// ---------- 2. PromptBuilder ----------
var cats = new List<Category>
{
    new() { Name = "自然", ParentId = null },
    new() { Name = "动物", ParentId = null },
};

var auto = PromptBuilder.BuildMessages("data:image/jpeg;base64,abc", ClassificationMode.Auto, cats, AiDetailLevel.Low);
Check(auto.Count == 2 && auto[0].Role == "system" && auto[1].Role == "user", "prompt: system + user messages");
Check(((string)auto[0].Content!).Contains("分类原则"), "prompt: base prompt present");
Check(!((string)auto[0].Content!).Contains("必须只从以上分类"), "prompt: auto mode has no fixed constraint");

var fixedMsgs = PromptBuilder.BuildMessages("data:image/jpeg;base64,abc", ClassificationMode.Fixed, cats, AiDetailLevel.Low);
Check(((string)fixedMsgs[0].Content!).Contains("必须只从以上分类中选择"), "prompt: fixed mode constraint");

var hybrid = PromptBuilder.BuildMessages("data:image/jpeg;base64,abc", ClassificationMode.Hybrid, cats, AiDetailLevel.Low);
Check(((string)hybrid[0].Content!).Contains("suggested_category"), "prompt: hybrid mode mentions suggested_category");

var parts = (List<DeepSeekContentPart>)auto[1].Content!;
Check(parts.Count == 1 && parts[0].Type == "image_url" && parts[0].ImageUrl!.Url == "data:image/jpeg;base64,abc" && parts[0].ImageUrl.Detail == "low",
    "prompt: user image_url part with low detail");

var orig = PromptBuilder.BuildMessages("data:image/png;base64,def", ClassificationMode.Auto, cats, AiDetailLevel.Original);
var origParts = (List<DeepSeekContentPart>)orig[1].Content!;
Check(origParts[0].ImageUrl!.Detail == "original", "prompt: original detail");

// ---------- 3. Classification persistence ----------
var repo = sp.GetRequiredService<IClassificationRepository>();
await repo.AddCategoryAsync(new Category { Name = "测试分类", ParentId = null, IsSystem = false, SortOrder = 0 });
var cat = await repo.GetCategoryByNameAsync("测试分类");
Check(cat is not null, "db: category created + fetched by name");

await repo.CreateTaskAsync(new ClassificationTask
{
    Status = TaskState.Completed,
    TotalCount = 1,
    CompletedCount = 1,
    FailedCount = 0,
    PendingCount = 0,
    StartedAt = DateTime.UtcNow,
    FinishedAt = DateTime.UtcNow,
});

// The summary query joins on Images, so create a real image row first.
await using var ctx = await dbFactory.CreateDbContextAsync();
var img = new Image
{
    FileName = "test.jpg",
    OriginalPath = "C:\\test.jpg",
    FileSize = 100,
    FileHash = "abc123",
    Width = 10,
    Height = 10,
    Format = ImageFormat.Jpeg,
    CreatedAt = DateTime.UtcNow,
    ModifiedAt = DateTime.UtcNow,
    ScanStatus = ImageScanStatus.Scanned,
    CreatedTime = DateTime.UtcNow,
};
ctx.Images.Add(img);
await ctx.SaveChangesAsync();
var imageId = img.Id;

await repo.SaveResultAsync(new ClassificationResult
{
    ImageId = imageId,
    CategoryId = cat!.Id,
    SubCategoryId = null,
    Description = "测试描述",
    Reason = "测试理由",
    Confidence = 0.9,
    TagsJson = """["标签"]""",
    NeedsReview = false,
    ModelName = "mock",
    PromptVersion = "v1",
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow,
});

var summaries = await repo.GetRecentResultSummariesAsync(10);
Check(summaries.Count >= 1, "db: result saved + summary returned");
if (summaries.Count >= 1)
{
    var s = summaries[0];
    Check(s.Description == "测试描述" && Math.Abs(s.Confidence - 0.9) < 0.0001 && s.CategoryName == "测试分类",
        "db: summary fields + category name resolution");
}

Console.WriteLine();
Console.WriteLine($"RESULT: {pass} pass, {fail} fail");
Environment.ExitCode = fail == 0 ? 0 : 1;
