using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;
using Serilog;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var dataDir = Path.Combine(repoRoot, ".testdata-api-smoke");
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

var ApiKey = Environment.GetEnvironmentVariable("PHOTOMIND_API_KEY")
    ?? throw new InvalidOperationException("请先设置 PHOTOMIND_API_KEY 环境变量。");
const string BaseUrl = "https://api.deepseek.com";
const string Model = "deepseek-flash";

var secretStore = sp.GetRequiredService<ISecretStore>();
var configStore = sp.GetRequiredService<IAiConfigStore>();
var client = sp.GetRequiredService<IDeepSeekClient>();

await secretStore.SaveSecretAsync(AiConstants.ApiKeySecretName, ApiKey);
await configStore.SaveAsync(new AiConfig
{
    BaseUrl = BaseUrl,
    Model = Model,
    AllowUpload = true,
    Detail = AiDetailLevel.Low,
    Concurrency = 1,
    MaxRetries = 2,
});

// ---- 1. Connectivity test (text-only) ----
Console.WriteLine("=== 1) 连通性测试（纯文本）===");
try
{
    var req = new DeepSeekRequest
    {
        Model = Model,
        Messages = new List<DeepSeekMessage> { new() { Role = "user", Content = "请只回复两个字：正常" } },
    };
    var resp = await client.ChatAsync(BaseUrl, ApiKey, req);
    Console.WriteLine($"OK  reply='{resp.Choices.FirstOrDefault()?.Message.Content}'  tokens_in={resp.Usage?.PromptTokens}  tokens_out={resp.Usage?.CompletionTokens}");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL  {ex.GetType().Name}: {ex.Message}");
}

// ---- 2. Image classification ----
Console.WriteLine("=== 2) 图片分类（photo-sunset.jpg）===");
var imagePath = Path.Combine(repoRoot, "TestData", "sample", "photo-sunset.jpg");
if (!File.Exists(imagePath))
{
    Console.WriteLine("缺失测试图: " + imagePath);
    Environment.ExitCode = 1;
    return;
}

await using var ctx = await dbFactory.CreateDbContextAsync();
var img = new Image
{
    FileName = "photo-sunset.jpg",
    OriginalPath = imagePath,
    FileSize = new FileInfo(imagePath).Length,
    FileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(imagePath))),
    Width = 480,
    Height = 320,
    Format = ImageFormat.Jpeg,
    CreatedAt = DateTime.UtcNow,
    ModifiedAt = DateTime.UtcNow,
    ScanStatus = ImageScanStatus.Scanned,
    CreatedTime = DateTime.UtcNow,
};
ctx.Images.Add(img);
await ctx.SaveChangesAsync();
var imageId = img.Id;

var classify = sp.GetRequiredService<IClassificationService>();
var pause = new PauseTokenSource();
var progress = new Progress<AiClassifyProgress>(p =>
{
    if (p.CurrentFile is not null)
        Console.WriteLine($"  [{p.Status}] {p.CompletedCount}/{p.TotalCount}  {p.CurrentFile}");
});

try
{
    var task = await classify.ClassifyAsync(
        new AiClassifyOptions { ImageIds = new long[] { imageId }, Mode = ClassificationMode.Auto },
        pause.Token,
        progress);
    Console.WriteLine($"task 状态={task.Status} 完成={task.CompletedCount} 失败={task.FailedCount}");
}
catch (Exception ex)
{
    Console.WriteLine($"CLASSIFY FAIL  {ex.GetType().Name}: {ex.Message}");
}

// ---- 3. Read back ----
Console.WriteLine("=== 3) 读回分类结果 ===");
var repo = sp.GetRequiredService<IClassificationRepository>();
var summaries = await repo.GetRecentResultSummariesAsync(10);
foreach (var s in summaries)
{
    Console.WriteLine($"  一级='{s.CategoryName}'  二级='{s.SubCategoryName}'  置信度={s.Confidence:P0}  待审核={s.NeedsReview}");
    Console.WriteLine($"  描述='{s.Description}'");
    Console.WriteLine($"  理由='{s.Reason}'");
    Console.WriteLine($"  标签={s.TagsJson}");
}

Console.WriteLine("DONE");
