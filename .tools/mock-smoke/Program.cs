using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;
using Serilog;

// Mock 验证：用假 IDeepSeekClient（返回真实 API 曾返回的 JSON）跑通"解析→分类→落库→用量"全链路，验证业务逻辑正确。
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var dataDir = Path.Combine(repoRoot, ".testdata-mock-smoke");
if (Directory.Exists(dataDir))
    Directory.Delete(dataDir, recursive: true);
Environment.SetEnvironmentVariable("PHOTOMIND_DATA_DIR", dataDir);

Log.Logger = new LoggerConfiguration().MinimumLevel.Information().CreateLogger();

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
services.RemoveAll<IDeepSeekClient>();
services.AddSingleton<IDeepSeekClient, FakeDeepSeekClient>();
await using var sp = services.BuildServiceProvider();

var paths = sp.GetRequiredService<IAppPaths>();
paths.EnsureCreated();
var dbFactory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(dbFactory);

var secretStore = sp.GetRequiredService<ISecretStore>();
var configStore = sp.GetRequiredService<IAiConfigStore>();
await secretStore.SaveSecretAsync(AiConstants.ApiKeySecretName, "fake-key");
await configStore.SaveAsync(new AiConfig
{
    BaseUrl = "https://api.deepseek.com",
    Model = "deepseek-flash",
    AllowUpload = true,
    Detail = AiDetailLevel.Low,
    Concurrency = 1,
    MaxRetries = 2,
});

var imagePath = Path.Combine(repoRoot, "TestData", "sample", "photo-sunset.jpg");
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
var task = await classify.ClassifyAsync(
    new AiClassifyOptions { ImageIds = new long[] { imageId }, Mode = ClassificationMode.Auto },
    pause.Token);
Console.WriteLine($"task 状态={task.Status} 完成={task.CompletedCount} 失败={task.FailedCount}");

var repo = sp.GetRequiredService<IClassificationRepository>();
var summaries = await repo.GetRecentResultSummariesAsync(10);
foreach (var s in summaries)
    Console.WriteLine($"一级='{s.CategoryName}' 二级='{s.SubCategoryName}' 置信度={s.Confidence:P0} 待审核={s.NeedsReview} 描述='{s.Description}' 标签={s.TagsJson}");

// 验证：分类被 Auto 模式新建 + 用量已记录
await using var verifyCtx = await dbFactory.CreateDbContextAsync();
var cats = verifyCtx.Categories.OrderBy(c => c.Id).ToList();
Console.WriteLine($"分类表: {string.Join(" | ", cats.Select(c => $"{c.Name}{(c.ParentId is null ? "" : "(子)")}"))}");
var usage = verifyCtx.ApiUsages.ToList();
foreach (var u in usage)
    Console.WriteLine($"用量: model={u.ModelName} in={u.InputTokens} out={u.OutputTokens} total={u.TotalTokens} estimated={u.IsEstimated}");

Console.WriteLine("DONE");

sealed class FakeDeepSeekClient : IDeepSeekClient
{
    private const string Json = "{\"category\":\"插画\",\"subcategory\":\"风景插画\",\"tags\":[\"日落\",\"海洋\",\"地平线\",\"渐变\",\"扁平风格\",\"极简\",\"黄昏\"],\"description\":\"一幅极简扁平风格的日落海景插画。\",\"reason\":\"画面由纯色渐变和几何图形构成，属于数字插画。\",\"confidence\":0.93,\"needs_review\":false,\"suggested_category\":null}";

    public Task<DeepSeekResponse> ChatAsync(string baseUrl, string apiKey, DeepSeekRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new DeepSeekResponse
        {
            Choices = new List<DeepSeekChoice>
            {
                new()
                {
                    Message = new DeepSeekResponseMessage { Role = "assistant", Content = Json },
                    FinishReason = "stop",
                },
            },
            Usage = new DeepSeekUsage { PromptTokens = 435, CompletionTokens = 443, TotalTokens = 878 },
        });
    }
}
