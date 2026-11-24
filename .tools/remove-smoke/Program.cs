using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

// --- isolated data dir + source images -------------------------------------
var baseDir = AppContext.BaseDirectory;
var dataDir = Path.Combine(baseDir, "smoke-data");
var sourceDir = Path.Combine(baseDir, "smoke-source");
if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
Directory.CreateDirectory(sourceDir);
Environment.SetEnvironmentVariable("PHOTOMIND_DATA_DIR", dataDir);

Generate(sourceDir, "a.jpg", 255, 0, 0);
Generate(sourceDir, "b.jpg", 0, 255, 0);
Generate(sourceDir, "c.jpg", 0, 0, 255);
Generate(sourceDir, "d.jpg", 255, 255, 0);
Generate(sourceDir, "e.jpg", 255, 0, 255);

int pass = 0, fail = 0;
void Check(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {name}");
    if (ok) pass++; else fail++;
}

// --- DI + DB init -----------------------------------------------------------
var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
await using var provider = services.BuildServiceProvider();

var paths = provider.GetRequiredService<IAppPaths>();
paths.EnsureCreated();
var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(factory);

var scanService = provider.GetRequiredService<IScanService>();
var scanRepo = provider.GetRequiredService<IScanRepository>();
var classRepo = provider.GetRequiredService<IClassificationRepository>();

// --- 1. scan ----------------------------------------------------------------
var options = new PhotoMind.Core.DTOs.ScanOptions { SourcePaths = new() { sourceDir }, Recursive = true };
var task1 = await scanService.ScanAsync(options, new PauseToken());
var images = await scanRepo.GetImagesByTaskAsync(task1.Id);
Check("scan produced 5 images", images.Count == 5);
var a = images.First(x => x.FileName == "a.jpg");
var b = images.First(x => x.FileName == "b.jpg");
var c = images.First(x => x.FileName == "c.jpg");
var d = images.First(x => x.FileName == "d.jpg");
var e = images.First(x => x.FileName == "e.jpg");

// --- 2. classify A and B (simulate completed classifications) ---------------
var cat = (await classRepo.GetCategoriesAsync()).First();
await classRepo.SaveResultAsync(new ClassificationResult
{
    ImageId = a.Id, CategoryId = cat.Id, Confidence = 0.9,
    Description = "A", Reason = "A", TagsJson = "[\"t1\"]",
    ModelName = "deepseek-flash", PromptVersion = "v1",
});
await classRepo.SaveResultAsync(new ClassificationResult
{
    ImageId = b.Id, CategoryId = cat.Id, Confidence = 0.8,
    Description = "B", Reason = "B", TagsJson = "[]",
    ModelName = "deepseek-flash", PromptVersion = "v1",
});

// --- 3/4. single + batch removal --------------------------------------------
var removedA = await scanRepo.MarkImagesSkippedAsync(new[] { a.Id });
Check("1. single removal persisted", removedA == 1);

var removedCD = await scanRepo.MarkImagesSkippedAsync(new[] { c.Id, d.Id });
Check("2. batch removal persisted", removedCD == 2);

// --- 3. task list refresh (B and E remain) -----------------------------------
var remaining = await scanRepo.GetImagesByTaskAsync(task1.Id);
Check("3. task list excludes removed (2 remain)",
    remaining.Count == 2 && remaining.All(x => x.Id == b.Id || x.Id == e.Id));

// --- 4. originals untouched ---------------------------------------------------
Check("4. originals still on disk",
    File.Exists(a.OriginalPath) && File.Exists(c.OriginalPath) && File.Exists(d.OriginalPath));

// --- 5/6. classification results unchanged ------------------------------------
await using (var db = await factory.CreateDbContextAsync())
{
    var results = await db.ClassificationResults
        .Where(r => r.ImageId == a.Id || r.ImageId == b.Id)
        .ToListAsync();
    Check("5. classification results count unchanged (2)", results.Count == 2);
    var aResult = results.FirstOrDefault(r => r.ImageId == a.Id);
    Check("6. A result intact (CategoryId + Confidence)",
        aResult is not null && aResult.CategoryId == cat.Id && aResult.Confidence == 0.9);
}

// --- 8. removed images excluded from unclassified queue -----------------------
var unclassified = await classRepo.GetUnclassifiedImageIdsAsync(int.MaxValue);
Check("8. removed excluded from unclassified queue (only E)",
    unclassified.Count == 1 && unclassified[0] == e.Id);

// --- 9. task1 status/counts intact ---------------------------------------------
var task1After = await scanRepo.GetTaskAsync(task1.Id);
Check("9. task1 status Completed + counts intact",
    task1After is not null && task1After.Status == TaskState.Completed
    && task1After.ScannedCount == 5 && task1After.TotalCount == 5 && task1After.FailedCount == 0);

// --- 7. re-scan finds all 5 again ------------------------------------------------
var task2 = await scanService.ScanAsync(options, new PauseToken());
var images2 = await scanRepo.GetImagesByTaskAsync(task2.Id);
Check("7. re-scan finds 5 images again", images2.Count == 5);

// --- 10. other tasks unaffected ---------------------------------------------------
Check("10. task2 images all Scanned (unaffected by prior removal)",
    images2.All(x => x.ScanStatus == ImageScanStatus.Scanned));
var task1Remaining = await scanRepo.GetImagesByTaskAsync(task1.Id);
Check("10b. task1 B/E still Scanned",
    task1Remaining.Count == 2 && task1Remaining.All(x => x.ScanStatus == ImageScanStatus.Scanned));

Console.WriteLine($"RESULT: {pass} PASS / {fail} FAIL");
return fail == 0 ? 0 : 1;

static void Generate(string dir, string name, byte r, byte g, byte b)
{
    var path = Path.Combine(dir, name);
    using var img = new SharpImage(32, 32, new Rgba32(r, g, b, 255));
    using var fs = File.Create(path);
    img.Save(fs, new JpegEncoder());
}
