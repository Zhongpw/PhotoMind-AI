using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

var repo = @"D:\AI_Agent+\dsh_next\PhotoMindAI";
var root = Path.Combine(repo, ".testdata-phase4-smoke");
var dataDir = Path.Combine(root, "data");
var sourceDir = Path.Combine(root, "source");
var destCopy = Path.Combine(root, "dest-copy");
var destMove = Path.Combine(root, "dest-move");
var destFolders = Path.Combine(root, "dest-folders");

// ---- setup ----
if (Directory.Exists(root)) Directory.Delete(root, true);
Directory.CreateDirectory(sourceDir);

Environment.SetEnvironmentVariable("PHOTOMIND_DATA_DIR", dataDir);

var sampleDir = Path.Combine(repo, "TestData", "sample");
var samples = new[] { "photo1.jpg", "photo2.png", "photo3.webp", "photo4.bmp", "photo5.gif" };
foreach (var s in samples)
    File.Copy(Path.Combine(sampleDir, s), Path.Combine(sourceDir, s));

// ---- DI + init ----
var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
var provider = services.BuildServiceProvider();

var paths = provider.GetRequiredService<IAppPaths>();
paths.EnsureCreated();
var dbFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(dbFactory);

// ---- scan ----
var scanService = provider.GetRequiredService<IScanService>();
var scanRepo = provider.GetRequiredService<IScanRepository>();
var scanTask = await scanService.ScanAsync(new ScanOptions { SourcePaths = new() { sourceDir }, Recursive = true }, new PauseTokenSource().Token);
var images = (await scanRepo.GetImagesByTaskAsync(scanTask.Id)).ToList();

// ---- classify ----
var classRepo = provider.GetRequiredService<IClassificationRepository>();
var categories = await classRepo.GetCategoriesAsync();
var topByName = categories.Where(c => c.ParentId == null).ToDictionary(c => c.Name, c => c.Id);
var subKey = new Dictionary<string, long>();
foreach (var c in categories.Where(c => c.ParentId != null))
{
    var parent = categories.First(p => p.Id == c.ParentId);
    subKey[parent.Name + "/" + c.Name] = c.Id;
}
long Top(string n) => topByName[n];
long Sub(string top, string n) => subKey[top + "/" + n];

var assign = new Dictionary<string, (long top, long? sub)>
{
    ["photo1.jpg"] = (Top("自然"), Sub("自然", "海洋")),
    ["photo2.png"] = (Top("自然"), Sub("自然", "山川")),
    ["photo3.webp"] = (Top("动物"), Sub("动物", "猫")),
    ["photo4.bmp"] = (Top("美食"), null),
    ["photo5.gif"] = (Top("生活"), Sub("生活", "日常")),
};

var results = new List<ClassificationResult>();
foreach (var img in images)
{
    var (t, s) = assign[img.FileName];
    results.Add(new ClassificationResult
    {
        ImageId = img.Id,
        CategoryId = t,
        SubCategoryId = s,
        Description = "测试",
        Reason = "测试",
        Confidence = 0.95,
        TagsJson = "[]",
        NeedsReview = false,
        ModelName = "test",
        PromptVersion = "v1",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    });
}
await classRepo.SaveResultsAsync(results);

// ---- services ----
var organize = provider.GetRequiredService<IOrganizeService>();
var hash = provider.GetRequiredService<IFileHashService>();

int pass = 0, fail = 0;
void Check(bool cond, string name)
{
    if (cond) { pass++; Console.WriteLine($"  OK  {name}"); }
    else { fail++; Console.WriteLine($"  FAIL {name}"); }
}
int Files(string dir) => Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count() : 0;
string[] AllFiles(string dir) => Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).OrderBy(x => x).ToArray() : Array.Empty<string>();

var srcHashesBefore = images.ToDictionary(i => i.FileName, i => i.OriginalPath);
var srcHashBefore = new Dictionary<string, string>();
foreach (var i in images) srcHashBefore[i.FileName] = await hash.ComputeSha256Async(i.OriginalPath);

Console.WriteLine("=== TEST 1: Preview (Copy) ===");
var preview = await organize.PreviewAsync(new OrganizeOptions { DestinationPath = destCopy, OperationType = OrganizeOperationType.Copy });
Check(preview.TotalCount == 5, $"total=5 (got {preview.TotalCount})");
Check(preview.CategoryCount == 4, $"4 top categories (got {preview.CategoryCount})");
Check(preview.FolderCount == 5, $"5 folders (got {preview.FolderCount})");
Check(preview.ConflictCount == 0, $"0 conflicts (got {preview.ConflictCount})");
Check(preview.ErrorCount == 0, $"0 errors (got {preview.ErrorCount})");

Console.WriteLine("=== TEST 2: Execute Copy ===");
var copyTask = await organize.ExecuteAsync(new OrganizeOptions { DestinationPath = destCopy, OperationType = OrganizeOperationType.Copy }, new PauseTokenSource().Token);
Check(copyTask.Status == TaskState.Completed, $"copy status=Completed (got {copyTask.Status})");
Check(copyTask.CompletedCount == 5, $"copy completed=5 (got {copyTask.CompletedCount})");
Check(copyTask.FailedCount == 0, $"copy failed=0 (got {copyTask.FailedCount})");
Check(Files(sourceDir) == 5, "source still has 5 files (copy must not delete originals)");
Check(Files(destCopy) == 5, $"dest has 5 files (got {Files(destCopy)})");
var destFile = AllFiles(destCopy).FirstOrDefault(f => f.EndsWith("photo1.jpg"));
Check(destFile != null && await hash.ComputeSha256Async(destFile) == srcHashBefore["photo1.jpg"], "copied photo1 hash matches source");

Console.WriteLine("=== TEST 3: Execute Copy again (conflict) ===");
var copyTask2 = await organize.ExecuteAsync(new OrganizeOptions { DestinationPath = destCopy, OperationType = OrganizeOperationType.Copy }, new PauseTokenSource().Token);
Check(copyTask2.CompletedCount == 5, $"copy2 completed=5 (got {copyTask2.CompletedCount})");
Check(Files(destCopy) == 10, $"dest has 10 files after conflict (got {Files(destCopy)})");
Check(AllFiles(destCopy).Any(f => Path.GetFileName(f) == "photo1_1.jpg"), "conflict resolved to photo1_1.jpg");

Console.WriteLine("=== TEST 4: Undo copy2 ===");
var undo2 = await organize.UndoAsync(copyTask2.Id, new PauseTokenSource().Token);
Check(undo2.Success && undo2.RevertedCount == 5, $"undo2 success reverts=5 (got {undo2.RevertedCount}/{undo2.FailedCount})");
Check(Files(destCopy) == 5, $"dest back to 5 files (got {Files(destCopy)})");
Check(Files(sourceDir) == 5, "source still has 5 files");

Console.WriteLine("=== TEST 5: Execute Move ===");
var moveTask = await organize.ExecuteAsync(new OrganizeOptions { DestinationPath = destMove, OperationType = OrganizeOperationType.Move }, new PauseTokenSource().Token);
Check(moveTask.Status == TaskState.Completed, $"move status=Completed (got {moveTask.Status})");
Check(moveTask.CompletedCount == 5 && moveTask.FailedCount == 0, $"move 5/0 (got {moveTask.CompletedCount}/{moveTask.FailedCount})");
Check(Files(sourceDir) == 0, $"source empty after move (got {Files(sourceDir)})");
Check(Files(destMove) == 5, $"destMove has 5 files (got {Files(destMove)})");
var movedFile = AllFiles(destMove).FirstOrDefault(f => f.EndsWith("photo1.jpg"));
Check(movedFile != null && await hash.ComputeSha256Async(movedFile) == srcHashBefore["photo1.jpg"], "moved photo1 hash matches original");

Console.WriteLine("=== TEST 6: Undo Move ===");
var undoMove = await organize.UndoAsync(moveTask.Id, new PauseTokenSource().Token);
Check(undoMove.Success && undoMove.RevertedCount == 5, $"undoMove reverts=5 (got {undoMove.RevertedCount}/{undoMove.FailedCount})");
Check(Files(sourceDir) == 5, $"source restored to 5 (got {Files(sourceDir)})");
Check(Files(destMove) == 0, $"destMove empty after undo (got {Files(destMove)})");
var restored = images.ToDictionary(i => i.FileName, i => i.OriginalPath);
Check(await hash.ComputeSha256Async(restored["photo1.jpg"]) == srcHashBefore["photo1.jpg"], "restored photo1 hash matches original");

Console.WriteLine("=== TEST 7: Execute CreateFoldersOnly ===");
var foldersTask = await organize.ExecuteAsync(new OrganizeOptions { DestinationPath = destFolders, OperationType = OrganizeOperationType.CreateFoldersOnly }, new PauseTokenSource().Token);
Check(foldersTask.CompletedCount == 5, $"folders-only completed=5 (got {foldersTask.CompletedCount})");
Check(Files(destFolders) == 0, $"no files created (got {Files(destFolders)})");
Check(Directory.Exists(Path.Combine(destFolders, "自然", "海洋")), "folder 自然\\海洋 created");
Check(Directory.Exists(Path.Combine(destFolders, "美食")), "folder 美食 created");
Check(Files(sourceDir) == 5, "source untouched (5 files)");

Console.WriteLine($"\nVERIFY: {(fail == 0 ? "PASS" : "FAIL")} ({pass}/{pass + fail})");
return fail == 0 ? 0 : 1;
