using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.AI;
using PhotoMind.Infrastructure.Database;
using PhotoMind.Infrastructure.ImageProcessing;
using PhotoMind.Infrastructure.Services;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

var pass = 0;
var fail = 0;

void Check(string name, bool ok, string? detail = null)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail is null ? "" : "  [" + detail + "]")}");
    if (ok) pass++; else fail++;
}

var dataDir = Environment.GetEnvironmentVariable("PHOTOMIND_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDir))
{
    Console.WriteLine("FATAL: PHOTOMIND_DATA_DIR not set");
    return 1;
}
Directory.CreateDirectory(dataDir);

// Model files must already be placed in <dataDir>/models/clip by the runner.
var clipDir = Path.Combine(dataDir, "models", "clip");
var modelOk = File.Exists(Path.Combine(clipDir, "vision_model.onnx"))
           && File.Exists(Path.Combine(clipDir, "text_model.onnx"))
           && File.Exists(Path.Combine(clipDir, "vocab.json"))
           && File.Exists(Path.Combine(clipDir, "merges.txt"));
Check("model files present", modelOk, clipDir);
if (!modelOk)
{
    Console.WriteLine($"RESULT: {pass} PASS / {fail} FAIL");
    return fail == 0 ? 0 : 1;
}

// --- Build DI, initialize DB + seed categories. ---
var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
var provider = services.BuildServiceProvider();

var paths = provider.GetRequiredService<IAppPaths>();
paths.EnsureCreated();

var factory = provider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(factory);

var catRepo = provider.GetRequiredService<IClassificationRepository>();
var categories = (await catRepo.GetCategoriesAsync()).ToList();
Check("categories seeded", categories.Count >= 6, $"count={categories.Count}");

var clip = provider.GetRequiredService<IClipModelService>();

// --- Phase 1: CPU inference (default config, GpuAcceleration=false). ---
try
{
    await clip.EnsureLoadedAsync();
    Check("model loaded", clip.State == ClipModelState.Ready, clip.State.ToString());
    Check("device is CPU", clip.Device == InferenceDevice.Cpu, clip.DeviceDescription);
    Check("not degraded (CPU requested)", !clip.Degraded, clip.DegradeReason ?? "");

    // Generate two test images.
    var imgDir = Path.Combine(dataDir, "temp");
    Directory.CreateDirectory(imgDir);
    var bluePath = Path.Combine(imgDir, "blue.jpg");
    var greenPath = Path.Combine(imgDir, "green.jpg");
    WriteJpeg(bluePath, 224, 224, new Rgb24(70, 130, 200));
    WriteJpeg(greenPath, 224, 224, new Rgb24(80, 170, 80));

    // Image encoding.
    var blueEmb = await clip.EncodeImageAsync(bluePath);
    var greenEmb = await clip.EncodeImageAsync(greenPath);
    Check("image embedding dim 512", blueEmb.Length == 512 && greenEmb.Length == 512, $"len={blueEmb.Length}");
    var blueNorm = Norm(blueEmb);
    Check("image embedding normalized", Math.Abs(blueNorm - 1.0) < 0.01, $"norm={blueNorm:F4}");

    // Text encoding + similarity.
    var catText = await clip.EncodeTextAsync("a photo of a cat");
    var carText = await clip.EncodeTextAsync("a photo of a car");
    Check("text embedding dim 512", catText.Length == 512 && carText.Length == 512, $"len={catText.Length}");
    Check("text embedding normalized", Math.Abs(Norm(catText) - 1.0) < 0.01, $"norm={Norm(catText):F4}");

    var self = Dot(catText, catText);
    var cross = Dot(catText, carText);
    Check("self similarity ~1", self > 0.99, $"self={self:F4}");
    Check("cross similarity < self", cross < self, $"cross={cross:F4}");

    // Hierarchical match (temperature 0.07).
    var match = await clip.MatchAsync(blueEmb, categories, 0.07);
    Check("match has category", match.CategoryId > 0, $"catId={match.CategoryId} name={match.CategoryName}");
    Check("match confidence in (0,1]", match.Confidence > 0 && match.Confidence <= 1, $"conf={match.Confidence:F4}");

    // Warmup (pre-encodes all category labels) + cache.
    await clip.WarmupAsync(categories);
    var cachePath = paths.ClipTextCacheFilePath;
    Check("text cache persisted", File.Exists(cachePath) && new FileInfo(cachePath).Length > 0, cachePath);
}
catch (Exception ex)
{
    Check("phase 1 CPU inference", false, ex.GetType().Name + ": " + ex.Message);
}

// --- Phase 2: GPU acceleration request -> expected graceful CPU fallback (no GPU here). ---
try
{
    var gpuConfig = new AiConfig { GpuAcceleration = true };
    var fakeStore = new FakeConfigStore(gpuConfig);
    var gpuClip = new ClipModelService(
        new AppPaths(),
        new ImageFormatDetector(),
        fakeStore,
        Log.Logger);

    await gpuClip.EnsureLoadedAsync();
    Check("GPU-requested loads", gpuClip.State == ClipModelState.Ready, gpuClip.State.ToString());
    Check("GPU-requested falls back to CPU", gpuClip.Device == InferenceDevice.Cpu, gpuClip.DeviceDescription);
    Check("GPU-requested marks degraded", gpuClip.Degraded, gpuClip.DegradeReason ?? "");
    Check("degrade reason present", !string.IsNullOrWhiteSpace(gpuClip.DegradeReason));
}
catch (Exception ex)
{
    Check("phase 2 GPU fallback", false, ex.GetType().Name + ": " + ex.Message);
}

Console.WriteLine($"RESULT: {pass} PASS / {fail} FAIL");
return fail == 0 ? 0 : 1;

static void WriteJpeg(string path, int w, int h, Rgb24 color)
{
    using var img = new Image<Rgb24>(w, h, color);
    using var fs = File.Create(path);
    img.Save(fs, new JpegEncoder());
}

static double Norm(float[] v)
{
    var sum = 0.0;
    foreach (var x in v) sum += (double)x * x;
    return Math.Sqrt(sum);
}

static double Dot(float[] a, float[] b)
{
    var sum = 0.0;
    for (var i = 0; i < a.Length; i++) sum += (double)a[i] * b[i];
    return sum;
}

sealed class FakeConfigStore : IAiConfigStore
{
    private readonly AiConfig _config;
    public FakeConfigStore(AiConfig config) => _config = config;
    public Task<AiConfig> LoadAsync(CancellationToken ct = default) => Task.FromResult(_config);
    public Task SaveAsync(AiConfig config, CancellationToken ct = default) => Task.CompletedTask;
}
