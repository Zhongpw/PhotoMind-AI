using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

var root = args.Length > 0 ? args[0] : @"D:\AI_Agent+\dsh_next\PhotoMindAI";
var source = args.Length > 1 ? args[1] : Path.Combine(root, "TestData", "perf10k");

// Use an isolated data dir for the perf run.
var dataDir = Path.Combine(root, ".testdata-perf10k");
if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true);
Environment.SetEnvironmentVariable(AppConstants.DataDirEnvironmentVariable, dataDir);

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
await using var sp = services.BuildServiceProvider();

var paths = sp.GetRequiredService<IAppPaths>();
paths.EnsureCreated();

var dbFactory = sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(dbFactory);

var scan = sp.GetRequiredService<IScanService>();
var repo = sp.GetRequiredService<IScanRepository>();

var opts = new ScanOptions
{
    SourcePaths = new List<string> { source },
    Recursive = true,
    AllowedFormats = new List<ImageFormat> { ImageFormat.Jpeg },
};

Console.WriteLine($"Scanning {source} ...");
var sw = Stopwatch.StartNew();
long peakWorkingSet = 0;
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
var progress = new Progress<ScanProgress>(p =>
{
    var ws = Process.GetCurrentProcess().WorkingSet64;
    if (ws > peakWorkingSet) peakWorkingSet = ws;
});

var task = await scan.ScanAsync(opts, new PhotoMind.Core.Threading.PauseToken(), progress, cts.Token);
sw.Stop();

var count = await repo.GetImageCountAsync(CancellationToken.None);
var recent = await repo.GetRecentScanTasksAsync(1, CancellationToken.None);
var t = recent.FirstOrDefault();

Console.WriteLine($"Scan task status = {task.Status}");
Console.WriteLine($"DB image count = {count}");
Console.WriteLine($"Total={task.TotalCount} Scanned={task.ScannedCount} Failed={task.FailedCount}");
Console.WriteLine($"Elapsed = {sw.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"PeakWorkingSet64 = {peakWorkingSet / (1024.0 * 1024.0):F1} MB");

// Also measure a paged query (DB response on 10k rows).
var sw2 = Stopwatch.StartNew();
var page = await repo.GetImagesAsync(9900, 100, CancellationToken.None);
sw2.Stop();
Console.WriteLine($"Paged query (skip 9900 take 100) = {page.Count} rows in {sw2.Elapsed.TotalMilliseconds:F1} ms");
