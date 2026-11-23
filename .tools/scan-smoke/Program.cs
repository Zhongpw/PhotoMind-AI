using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

var sampleDir = args.Length > 0 ? args[0] : "TestData/sample";
sampleDir = Path.GetFullPath(sampleDir);
Console.WriteLine($"Scanning: {sampleDir}");

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
await using var provider = services.BuildServiceProvider();

var paths = provider.GetRequiredService<IAppPaths>();
paths.EnsureCreated();
Console.WriteLine($"Data dir: {paths.DataDirectory}");

var dbFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(dbFactory);

var scanService = provider.GetRequiredService<IScanService>();
var repo = provider.GetRequiredService<IScanRepository>();

var options = new ScanOptions
{
    SourcePaths = new List<string> { sampleDir },
    Recursive = true,
};

var pause = new PauseTokenSource();
var progress = new Progress<ScanProgress>(p =>
    Console.WriteLine($"  progress: total={p.TotalCount} scanned={p.ScannedCount} failed={p.FailedCount} current={p.CurrentFile}"));

var task = await scanService.ScanAsync(options, pause.Token, progress, CancellationToken.None);

Console.WriteLine();
Console.WriteLine($"RESULT task.Id={task.Id} status={task.Status} total={task.TotalCount} scanned={task.ScannedCount} failed={task.FailedCount}");

var images = await repo.GetImagesByTaskAsync(task.Id);
Console.WriteLine($"DB image rows for this task = {images.Count}");
foreach (var img in images)
{
    var hashShort = img.FileHash.Length >= 12 ? img.FileHash[..12] : img.FileHash;
    var thumb = img.ThumbnailPath is null ? "NULL" : (File.Exists(img.ThumbnailPath) ? "exists" : "MISSING");
    Console.WriteLine($"  id={img.Id,-3} status={img.ScanStatus,-7} fmt={img.Format,-7} {img.Width,4}x{img.Height,-4} hash={hashShort}... thumb={thumb} name={img.FileName}");
}

var scanned = images.Count(i => i.ScanStatus == ImageScanStatus.Scanned);
var failed = images.Count(i => i.ScanStatus == ImageScanStatus.Failed);
var skipped = task.TotalCount - scanned - failed;
Console.WriteLine();
Console.WriteLine($"SUMMARY scanned={scanned} failed={failed} skipped(not persisted)={skipped}");

// Expected: 6 valid images (jpg/png/webp/bmp/gif + nested png) scanned,
// 1 corrupt-JPEG-magic file failed, 2 skipped (tiff unsupported + unknown-magic broken.jpg).
var ok = task.Status == TaskState.Completed && scanned == 6 && failed == 1 && skipped == 2;
Console.WriteLine(ok ? "VERIFY: PASS" : "VERIFY: FAIL");
return ok ? 0 : 1;
