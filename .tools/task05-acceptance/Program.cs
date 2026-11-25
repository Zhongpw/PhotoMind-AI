using System.Security.Cryptography;
using System.Text;
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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

// Task 05 acceptance harness: exercises the REAL application service pipeline (real DI, real CLIP
// ONNX model, real SQLite, real repositories) end-to-end, with only the DeepSeek HTTP client faked
// (a scripted response — no real API key or quota is consumed). This verifies the actual
// classification flow the WPF app runs, including candidate screening, fallbacks, caching, DB
// records, and that original photos are never modified.
//
// Usage: task05-acceptance.exe   (PHOTOMIND_DATA_DIR must point at a data dir with models/clip populated)

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) { pass++; Console.WriteLine($"  PASS  {name}"); }
    else { fail++; Console.WriteLine($"  FAIL  {name}" + (string.IsNullOrEmpty(detail) ? "" : $"  [{detail}]")); }
}

var dataDir = Environment.GetEnvironmentVariable("PHOTOMIND_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDir))
{
    Console.WriteLine("FATAL: PHOTOMIND_DATA_DIR not set");
    return 1;
}
Directory.CreateDirectory(dataDir);

// ---- Two-phase DI: resolve the real CLIP service first, then wrap it in a counting/fault decorator. ----
var baseServices = new ServiceCollection();
baseServices.AddPhotoMindInfrastructure();
var baseProvider = baseServices.BuildServiceProvider();
var realClip = baseProvider.GetRequiredService<IClipModelService>();

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
services.AddSingleton<IDeepSeekClient, FakeDeepSeekClient>();
services.AddSingleton<IClipModelService>(new TogglableClipModelService(realClip));
var provider = services.BuildServiceProvider();

var paths = provider.GetRequiredService<IAppPaths>();
paths.EnsureCreated();
var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(factory);

var catRepo = provider.GetRequiredService<IClassificationRepository>();
var categories = (await catRepo.GetCategoriesAsync()).ToList();
var topLevel = categories.Where(c => c.ParentId is null).OrderBy(c => c.Id).ToList();

var secretStore = provider.GetRequiredService<ISecretStore>();
await secretStore.SaveSecretAsync(AiConstants.ApiKeySecretName, "sk-test-acceptance-dummy");

var configStore = provider.GetRequiredService<IAiConfigStore>();
await configStore.SaveAsync(new AiConfig { AllowUpload = true });

var fakeClient = (FakeDeepSeekClient)provider.GetRequiredService<IDeepSeekClient>();
var togglableClip = (TogglableClipModelService)provider.GetRequiredService<IClipModelService>();
var classify = provider.GetRequiredService<IClassificationService>();
var scanService = provider.GetRequiredService<IScanService>();
var scanRepo = provider.GetRequiredService<IScanRepository>();

Console.WriteLine("=== 1. 配置默认值 ===");
Check("AiConfig 默认 ClipTopK == 5", new AiConfig().ClipTopK == 5, $"actual={new AiConfig().ClipTopK}");
Check("AiConstants.DefaultClipTopK == 5", AiConstants.DefaultClipTopK == 5);
Check("默认 ClipCandidateFiltering == false", !new AiConfig().ClipCandidateFiltering);
Check("默认 ClipConfidenceThreshold == 0.8", Math.Abs(new AiConfig().ClipConfidenceThreshold - 0.8) < 1e-9);

Console.WriteLine("=== 2. Top-K 切换（3/5/8/全部）round-trip ===");
foreach (var k in new[] { 3, 5, 8, 0 })
{
    await configStore.SaveAsync(new AiConfig { AllowUpload = true, ClipTopK = k });
    var loaded = await configStore.LoadAsync();
    Check($"ClipTopK={k} 保存后可读回", loaded.ClipTopK == k, $"read={loaded.ClipTopK}");
}

Console.WriteLine("=== 3. 设置持久化（新 store 实例 = 重启） ===");
{
    await configStore.SaveAsync(new AiConfig
    {
        AllowUpload = true,
        ClipCandidateFiltering = true,
        ClipTopK = 5,
        SaveTokenMode = true,
        GpuAcceleration = false,
    });
    var fresh = new PhotoMind.Infrastructure.Services.AiConfigStore(paths);
    var loaded = await fresh.LoadAsync();
    Check("重启后 ClipCandidateFiltering=true 保持", loaded.ClipCandidateFiltering);
    Check("重启后 ClipTopK=5 保持", loaded.ClipTopK == 5);
    Check("重启后 SaveTokenMode=true 保持", loaded.SaveTokenMode);
    Check("重启后 GpuAcceleration=false 保持", !loaded.GpuAcceleration);
}

Console.WriteLine("=== 4. 生成并扫描 6 张测试照片 ===");
// Scan source must live OUTSIDE the app data dir (the scanner intentionally skips its own data/cache dirs).
var scanFolder = dataDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "-src";
Directory.CreateDirectory(scanFolder);
var colors = new (string Name, Rgb24 Color)[]
{
    ("imgA", new Rgb24(220, 40, 40)),   // red
    ("imgB", new Rgb24(40, 160, 40)),   // green
    ("imgC", new Rgb24(40, 60, 220)),   // blue
    ("imgD", new Rgb24(230, 210, 40)),  // yellow
    ("imgE", new Rgb24(160, 40, 200)),  // purple
    ("imgF", new Rgb24(40, 200, 200)),  // cyan
};
foreach (var (name, color) in colors)
{
    var p = Path.Combine(scanFolder, name + ".jpg");
    using (var img = new Image<Rgb24>(224, 224, color))
    using (var fs = File.Create(p))
        img.Save(fs, new JpegEncoder());
}
var scanTask = await scanService.ScanAsync(
    new ScanOptions { SourcePaths = new List<string> { scanFolder }, Recursive = false },
    new PauseToken());
var images = (await scanRepo.GetImagesByTaskAsync(scanTask.Id)).OrderBy(i => i.FileName).ToList();
Check("扫描 6 张照片", images.Count == 6, $"count={images.Count}");
if (images.Count == 0)
{
    Console.WriteLine("FATAL: scan produced 0 images — aborting remaining scenarios.");
    Console.WriteLine($"RESULT: {pass} PASS / {fail + 1} FAIL");
    return 1;
}
var origHashes = images.ToDictionary(i => i.Id, i => Sha256File(i.OriginalPath));

// ---- verify model copy exists before touching CLIP ----
var modelDir = paths.ClipModelDirectory;
Check("模型目录存在 vision_model.onnx", File.Exists(Path.Combine(modelDir, "vision_model.onnx")));
Check("模型目录存在 text_model.onnx", File.Exists(Path.Combine(modelDir, "text_model.onnx")));

Console.WriteLine("=== 5. 关闭候选筛选 → 原始完整提示词（基线） ===");
{
    await configStore.SaveAsync(new AiConfig { AllowUpload = true, ClipCandidateFiltering = false, SaveTokenMode = false });
    fakeClient.Reset();
    fakeClient.NextCategory = "动物"; fakeClient.NextSubcategory = "猫";
    togglableClip.Reset();
    var t = await classify.ClassifyAsync(new AiClassifyOptions { ImageIds = new[] { images[0].Id }, Mode = ClassificationMode.Hybrid }, new PauseToken());
    var sys = (string)fakeClient.LastRequest!.Messages[0].Content!;
    Check("关闭后 DeepSeek 被调用", fakeClient.CallCount >= 1);
    Check("关闭后 GetTopKCandidatesAsync 未调用", togglableClip.CandidatesCalls == 0, $"calls={togglableClip.CandidatesCalls}");
    Check("关闭后 MatchAsync 未调用", togglableClip.MatchCalls == 0, $"calls={togglableClip.MatchCalls}");
    Check("关闭后提示词无候选文本", !sys.Contains("本地图像模型已初步筛选"));
    Check("关闭后提示词含完整分类体系", sys.Contains("当前已有的分类体系"));
    var r = GetResults(factory, images[0].Id);
    Check("关闭后 CandidateCount=null", r[0].CandidateCount is null, $"val={r[0].CandidateCount}");
    Check("关闭后 CandidateHit=null", r[0].CandidateHit is null);
    Check("关闭后 Source=DeepSeek", r[0].Source == ClassificationSource.DeepSeek);
    Check("关闭后原图未被修改", Sha256File(images[0].OriginalPath) == origHashes[images[0].Id]);
}

Console.WriteLine("=== 6. 开启候选筛选 → CLIP 生成候选并传给 DeepSeek（命中） ===");
{
    await configStore.SaveAsync(new AiConfig { AllowUpload = true, ClipCandidateFiltering = true, ClipTopK = 3, SaveTokenMode = false });
    fakeClient.Reset();
    fakeClient.NextCategory = "动物"; fakeClient.NextSubcategory = "猫";   // a real category
    togglableClip.Reset();
    var t = await classify.ClassifyAsync(new AiClassifyOptions { ImageIds = new[] { images[1].Id }, Mode = ClassificationMode.Hybrid }, new PauseToken());
    var sys = (string)fakeClient.LastRequest!.Messages[0].Content!;
    Check("开启后 GetTopKCandidatesAsync 调用 1 次", togglableClip.CandidatesCalls == 1, $"calls={togglableClip.CandidatesCalls}");
    Check("开启后 EncodeImageAsync 只调用 1 次（复用）", togglableClip.EncodeCalls == 1, $"calls={togglableClip.EncodeCalls}");
    Check("开启后提示词含候选文本", sys.Contains("本地图像模型已初步筛选"));
    Check("开启后提示词仍含完整分类体系（兜底）", sys.Contains("当前已有的分类体系"));
    var r = GetResults(factory, images[1].Id);
    Check("开启后 CandidateCount=3", r[0].CandidateCount == 3, $"val={r[0].CandidateCount}");
    // CandidateHit true iff the returned category's branch is among the 3 candidates.
    var candBranchHit = (r[0].CandidateHit ?? false);
    Check("开启后 CandidateHit 有值", r[0].CandidateHit is not null);
    Console.WriteLine($"        （返回类别=动物，CandidateHit={r[0].CandidateHit}）");
    Check("开启后原图未被修改", Sha256File(images[1].OriginalPath) == origHashes[images[1].Id]);
}

Console.WriteLine("=== 7. DeepSeek 选择候选之外类别（不被 CLIP 绑架） ===");
{
    await configStore.SaveAsync(new AiConfig { AllowUpload = true, ClipCandidateFiltering = true, ClipTopK = 3, SaveTokenMode = false });
    // Compute candidates first, then pick a top-level category guaranteed NOT in them.
    var emb = await realClip.EncodeImageAsync(images[2].OriginalPath);
    var cands = await realClip.GetTopKCandidatesAsync(emb, categories, 3);
    var candBranches = cands.Select(c => c.ParentId ?? c.CategoryId).ToHashSet();
    var outside = topLevel.FirstOrDefault(tc => !candBranches.Contains(tc.Id));
    if (outside is null)
    {
        Console.WriteLine("  SKIP  (无法构造候选外类别)");
    }
    else
    {
        fakeClient.Reset();
        fakeClient.NextCategory = outside.Name; fakeClient.NextSubcategory = null;
        togglableClip.Reset();
        var t = await classify.ClassifyAsync(new AiClassifyOptions { ImageIds = new[] { images[2].Id }, Mode = ClassificationMode.Hybrid }, new PauseToken());
        var r = GetResults(factory, images[2].Id);
        Check("DeepSeek 选择候选外类别成功保存", r[0].CategoryId == outside.Id, $"cat={outside.Name}");
        Check("候选外选择 CandidateHit=false", r[0].CandidateHit == false, $"hit={r[0].CandidateHit}");
        Check("候选外选择仍写入 DB（非被拒）", r.Count == 1);
        Console.WriteLine($"        （CLIP 候选分支={string.Join(",", candBranches)}，DeepSeek 选了 {outside.Name}，CandidateHit={r[0].CandidateHit}）");
    }
}

Console.WriteLine("=== 8. CLIP 候选筛选失败 → 回退完整分类体系 ===");
{
    await configStore.SaveAsync(new AiConfig { AllowUpload = true, ClipCandidateFiltering = true, ClipTopK = 3, SaveTokenMode = false });
    fakeClient.Reset();
    fakeClient.NextCategory = "美食"; fakeClient.NextSubcategory = "甜点";
    togglableClip.Reset();
    togglableClip.FailOnCandidates = true;
    var t = await classify.ClassifyAsync(new AiClassifyOptions { ImageIds = new[] { images[3].Id }, Mode = ClassificationMode.Hybrid }, new PauseToken());
    togglableClip.FailOnCandidates = false;
    var sys = (string)fakeClient.LastRequest!.Messages[0].Content!;
    Check("候选失败后 DeepSeek 仍被调用", fakeClient.CallCount >= 1);
    Check("候选失败后提示词无候选文本", !sys.Contains("本地图像模型已初步筛选"));
    Check("候选失败后提示词含完整分类体系", sys.Contains("当前已有的分类体系"));
    var r = GetResults(factory, images[3].Id);
    Check("候选失败后 CandidateCount=null", r[0].CandidateCount is null);
    Check("候选失败后仍正常分类", r[0].CategoryId is not null);
}

Console.WriteLine("=== 9. 0.8 直接采信模式独立（不受候选筛选影响） ===");
{
    // 9a: threshold 0 → CLIP 直接采信，不调用 DeepSeek（证明 SaveTokenMode 独立工作）。
    await configStore.SaveAsync(new AiConfig { AllowUpload = true, ClipCandidateFiltering = false, SaveTokenMode = true, ClipConfidenceThreshold = 0.0 });
    fakeClient.Reset();
    togglableClip.Reset();
    var t = await classify.ClassifyAsync(new AiClassifyOptions { ImageIds = new[] { images[4].Id }, Mode = ClassificationMode.Hybrid }, new PauseToken());
    var r = GetResults(factory, images[4].Id);
    Check("直接采信：MatchAsync 调用", togglableClip.MatchCalls >= 1, $"calls={togglableClip.MatchCalls}");
    Check("直接采信：GetTopKCandidatesAsync 未调用", togglableClip.CandidatesCalls == 0);
    Check("直接采信：DeepSeek 未调用", fakeClient.CallCount == 0, $"calls={fakeClient.CallCount}");
    Check("直接采信：Source=Clip", r[0].Source == ClassificationSource.Clip, $"src={r[0].Source}");
    Check("直接采信：CandidateCount=null", r[0].CandidateCount is null);

    // 9b: threshold 0.99（不可达）→ CLIP 回退 DeepSeek（证明 0.8 门控仍有效）。
    await configStore.SaveAsync(new AiConfig { AllowUpload = true, ClipCandidateFiltering = false, SaveTokenMode = true, ClipConfidenceThreshold = 0.99 });
    fakeClient.Reset();
    fakeClient.NextCategory = "自然"; fakeClient.NextSubcategory = "海洋";
    togglableClip.Reset();
    var t2 = await classify.ClassifyAsync(new AiClassifyOptions { ImageIds = new[] { images[5].Id }, Mode = ClassificationMode.Hybrid }, new PauseToken());
    var r2 = GetResults(factory, images[5].Id);
    Check("高阈值回退：DeepSeek 被调用", fakeClient.CallCount >= 1, $"calls={fakeClient.CallCount}");
    Check("高阈值回退：Source=DeepSeek", r2[0].Source == ClassificationSource.DeepSeek);
}

Console.WriteLine("=== 10. 历史统计聚合 + 缓存 + 原图校验 ===");
{
    var stats = await catRepo.GetCandidateStatsAsync();
    // Screened images: images[1] (hit, candidate count 3), images[2] (miss if scenario 7 ran), images[3] (null → excluded).
    // images[0] (disabled) and images[4]/[5] (direct acceptance) are CandidateCount=null → excluded from screening stats.
    Check("候选统计 ScreenedCount >= 1", stats.ScreenedCount >= 1, $"n={stats.ScreenedCount}");
    Check("候选统计平均候选数 3.0", Math.Abs(stats.AverageCandidateCount - 3.0) < 1e-6, $"avg={stats.AverageCandidateCount}");
    Check("候选统计 Hit+Miss == Screened", stats.HitCount + stats.MissCount == stats.ScreenedCount,
        $"hit={stats.HitCount} miss={stats.MissCount} n={stats.ScreenedCount}");
    Console.WriteLine($"        stats = Screened={stats.ScreenedCount} AvgCand={stats.AverageCandidateCount:F2} Hit={stats.HitCount} Miss={stats.MissCount}");

    var cachePath = paths.ClipTextCacheFilePath;
    Check("CLIP 文本缓存已生成", File.Exists(cachePath) && new FileInfo(cachePath).Length > 0,
        $"exists={File.Exists(cachePath)} size={(File.Exists(cachePath) ? new FileInfo(cachePath).Length : 0)}");

    // All originals unchanged.
    var allUnchanged = true;
    foreach (var img in images)
        if (Sha256File(img.OriginalPath) != origHashes[img.Id]) { allUnchanged = false; break; }
    Check("全部 6 张原图未被修改", allUnchanged);

    // API usage recorded for every DeepSeek call (real tokens from the fake response).
    using var db = factory.CreateDbContext();
    var usageCount = db.ApiUsages.Count();
    Check("API 用量已记录", usageCount >= 1, $"count={usageCount}");
    Console.WriteLine($"        ApiUsage rows={usageCount}");
}

Console.WriteLine();
Console.WriteLine($"RESULT: {pass} PASS / {fail} FAIL");
return fail == 0 ? 0 : 1;

static string Sha256File(string path)
{
    using var sha = SHA256.Create();
    using var fs = File.OpenRead(path);
    return Convert.ToHexString(sha.ComputeHash(fs));
}

static List<ClassificationResult> GetResults(IDbContextFactory<AppDbContext> factory, long imageId)
{
    using var db = factory.CreateDbContext();
    return db.ClassificationResults.Where(r => r.ImageId == imageId).OrderByDescending(r => r.Id).ToList();
}

/// <summary>Scripted DeepSeek client: records the request and returns a fixed category (no network, no quota).</summary>
internal sealed class FakeDeepSeekClient : IDeepSeekClient
{
    public int CallCount { get; private set; }
    public DeepSeekRequest? LastRequest { get; private set; }
    public string? NextCategory { get; set; }
    public string? NextSubcategory { get; set; }

    public void Reset() { CallCount = 0; LastRequest = null; }

    public Task<DeepSeekResponse> ChatAsync(string baseUrl, string apiKey, DeepSeekRequest request, CancellationToken ct = default)
    {
        CallCount++;
        LastRequest = request;
        var cat = NextCategory ?? "其他";
        var sub = string.IsNullOrWhiteSpace(NextSubcategory) ? "null" : $"\"{NextSubcategory}\"";
        var json = $"{{\"category\":\"{cat}\",\"subcategory\":{sub},\"tags\":[],\"description\":\"test\",\"reason\":\"test\",\"confidence\":0.9,\"needs_review\":false,\"suggested_category\":null}}";
        var resp = new DeepSeekResponse
        {
            Choices = new List<DeepSeekChoice>
            {
                new() { Message = new DeepSeekResponseMessage { Role = "assistant", Content = json }, FinishReason = "stop" },
            },
            Usage = new DeepSeekUsage { PromptTokens = 150, CompletionTokens = 120, TotalTokens = 270 },
        };
        return Task.FromResult(resp);
    }
}

/// <summary>Counting/fault-injecting decorator over the real CLIP service (verifies reuse + fallback).</summary>
internal sealed class TogglableClipModelService : IClipModelService
{
    private readonly IClipModelService _inner;
    public int EncodeCalls { get; private set; }
    public int CandidatesCalls { get; private set; }
    public int MatchCalls { get; private set; }
    public int WarmupCalls { get; private set; }
    public bool FailOnEncode { get; set; }
    public bool FailOnCandidates { get; set; }

    public TogglableClipModelService(IClipModelService inner) => _inner = inner;

    public void Reset() { EncodeCalls = CandidatesCalls = MatchCalls = WarmupCalls = 0; FailOnEncode = FailOnCandidates = false; }

    public ClipModelState State => _inner.State;
    public InferenceDevice Device => _inner.Device;
    public string DeviceDescription => _inner.DeviceDescription;
    public string? LastError => _inner.LastError;
    public bool Degraded => _inner.Degraded;
    public string? DegradeReason => _inner.DegradeReason;

    public Task EnsureLoadedAsync(CancellationToken ct = default) => _inner.EnsureLoadedAsync(ct);

    public Task<float[]> EncodeImageAsync(string imagePath, CancellationToken ct = default)
    {
        EncodeCalls++;
        if (FailOnEncode) throw new InvalidOperationException("simulated CLIP encode failure");
        return _inner.EncodeImageAsync(imagePath, ct);
    }

    public Task<float[]> EncodeTextAsync(string text, CancellationToken ct = default) => _inner.EncodeTextAsync(text, ct);

    public Task WarmupAsync(IReadOnlyList<Category> categories, CancellationToken ct = default)
    {
        WarmupCalls++;
        return _inner.WarmupAsync(categories, ct);
    }

    public Task<ClipMatchResult> MatchAsync(float[] imageEmbedding, IReadOnlyList<Category> categories, double temperature, CancellationToken ct = default)
    {
        MatchCalls++;
        return _inner.MatchAsync(imageEmbedding, categories, temperature, ct);
    }

    public Task<IReadOnlyList<ClipCandidate>> GetTopKCandidatesAsync(float[] imageEmbedding, IReadOnlyList<Category> categories, int topK, CancellationToken ct = default)
    {
        CandidatesCalls++;
        if (FailOnCandidates) throw new InvalidOperationException("simulated candidate-screening failure");
        return _inner.GetTopKCandidatesAsync(imageEmbedding, categories, topK, ct);
    }
}
