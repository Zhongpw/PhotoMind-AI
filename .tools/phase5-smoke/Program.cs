using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

// Isolate every run in its own data directory under the harness output.
var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
if (Directory.Exists(dataDir))
    Directory.Delete(dataDir, recursive: true);
Environment.SetEnvironmentVariable("PHOTOMIND_DATA_DIR", dataDir);

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
await using var provider = services.BuildServiceProvider();

var paths = provider.GetRequiredService<IAppPaths>();
paths.EnsureCreated();

var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(factory);

int pass = 0, fail = 0;
void Check(string name, bool ok)
{
    if (ok) { pass++; Console.WriteLine($"PASS  {name}"); }
    else { fail++; Console.WriteLine($"FAIL  {name}"); }
}

// ---------- Seed ----------
await using (var db = await factory.CreateDbContextAsync())
{
    var now = DateTime.UtcNow;
    db.Images.AddRange(
        new Image { FileName = "dup1.jpg", OriginalPath = @"C:\a\dup1.jpg", FileHash = "AAA", FileSize = 100, Width = 8, Height = 8, Format = ImageFormat.Jpeg, CreatedAt = now, ModifiedAt = now, ScanStatus = ImageScanStatus.Scanned },
        new Image { FileName = "dup2.jpg", OriginalPath = @"C:\b\dup2.jpg", FileHash = "AAA", FileSize = 100, Width = 8, Height = 8, Format = ImageFormat.Jpeg, CreatedAt = now, ModifiedAt = now, ScanStatus = ImageScanStatus.Scanned },
        new Image { FileName = "unique.jpg", OriginalPath = @"C:\c\unique.jpg", FileHash = "BBB", FileSize = 200, Width = 8, Height = 8, Format = ImageFormat.Jpeg, CreatedAt = now, ModifiedAt = now, ScanStatus = ImageScanStatus.Scanned });

    db.ScanTasks.Add(new ScanTask { SourcePath = "[\"C:\\\\photos\"]", Status = TaskState.Running, TotalCount = 10, ScannedCount = 5, StartedAt = now.AddMinutes(-10) });
    db.ClassificationTasks.Add(new ClassificationTask { Status = TaskState.Paused, TotalCount = 5, CompletedCount = 2, StartedAt = now.AddMinutes(-8) });
    db.ClassificationTasks.Add(new ClassificationTask { Status = TaskState.Completed, TotalCount = 5, CompletedCount = 5, StartedAt = now.AddMinutes(-5), FinishedAt = now });
    db.OrganizeTasks.Add(new OrganizeTask { DestinationPath = @"D:\out", OperationType = OrganizeOperationType.Copy, Status = TaskState.Pending, TotalCount = 3, StartedAt = now.AddMinutes(-2) });

    db.ApiUsages.AddRange(
        new ApiUsage { ModelName = "deepseek-flash", InputTokens = 100, OutputTokens = 50, TotalTokens = 150, EstimatedCost = 0.001m, IsEstimated = false, RequestStatus = "ok", CreatedAt = now },
        new ApiUsage { ModelName = "deepseek-flash", InputTokens = 200, OutputTokens = 100, TotalTokens = 300, EstimatedCost = 0.002m, IsEstimated = true, RequestStatus = "ok", CreatedAt = now });

    await db.SaveChangesAsync();
}

// ---------- 1. Task recovery ----------
var recovery = provider.GetRequiredService<ITaskRecoveryService>();
var result = await recovery.RecoverInterruptedTasksAsync();
Check("recovery counts (1 scan + 1 class + 1 organize)", result.RecoveredScanTasks == 1 && result.RecoveredClassificationTasks == 1 && result.RecoveredOrganizeTasks == 1 && result.TotalRecovered == 3);

await using (var db = await factory.CreateDbContextAsync())
{
    var scans = await db.ScanTasks.ToListAsync();
    Check("scan task -> Failed", scans.Count == 1 && scans[0].Status == TaskState.Failed && scans[0].FinishedAt is not null);

    var classes = await db.ClassificationTasks.ToListAsync();
    Check("classification: paused->Failed, completed stays Completed", classes.Count(c => c.Status == TaskState.Failed) == 1 && classes.Count(c => c.Status == TaskState.Completed) == 1);

    var orgs = await db.OrganizeTasks.ToListAsync();
    Check("organize task -> Failed", orgs.Count == 1 && orgs[0].Status == TaskState.Failed);
}

var result2 = await recovery.RecoverInterruptedTasksAsync();
Check("recovery idempotent (second run recovers 0)", result2.TotalRecovered == 0);

// ---------- 2. Duplicate detection ----------
var scanRepo = provider.GetRequiredService<IScanRepository>();
var dups = await scanRepo.GetDuplicateGroupsAsync();
Check("duplicate groups = 1 (AAA x2)", dups.Count == 1 && dups[0].FileHash == "AAA" && dups[0].Count == 2 && dups[0].Images.Count == 2);

// ---------- 3. API usage summary ----------
var classRepo = provider.GetRequiredService<IClassificationRepository>();
var usage = await classRepo.GetUsageSummaryAsync();
Check("usage aggregation (2 req / 300 in / 150 out / 450 total / 1 estimated)",
    usage.RequestCount == 2 && usage.InputTokens == 300 && usage.OutputTokens == 150 && usage.TotalTokens == 450 && usage.EstimatedCount == 1);

// ---------- 4. Recent task lists ----------
var recentClass = await classRepo.GetRecentClassificationTasksAsync(10);
Check("recent classification tasks = 2", recentClass.Count == 2);

var organizeRepo = provider.GetRequiredService<IOrganizeRepository>();
var recentOrg = await organizeRepo.GetRecentOrganizeTasksAsync(10);
Check("recent organize tasks = 1", recentOrg.Count == 1);

var recentScan = await scanRepo.GetRecentScanTasksAsync(10);
Check("recent scan tasks = 1", recentScan.Count == 1);

Console.WriteLine();
Console.WriteLine($"RESULT: {pass} PASS / {fail} FAIL");
return fail == 0 ? 0 : 1;
