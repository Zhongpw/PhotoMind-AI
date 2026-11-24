using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Interfaces;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

// Honest small validation of the local CLIP zero-shot router.
// Images are real photos downloaded from Wikimedia Commons, grouped into folders named by
// the category's English alias (the folder name IS the ground-truth label).

var dataDir = Environment.GetEnvironmentVariable("PHOTOMIND_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDir))
{
    Console.WriteLine("FATAL: PHOTOMIND_DATA_DIR not set");
    return 1;
}
Directory.CreateDirectory(dataDir);

var evalDir = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("CLIP_EVAL_DIR") ?? ".\\validation";
if (!Directory.Exists(evalDir))
{
    Console.WriteLine($"FATAL: validation dir not found: {evalDir}");
    return 1;
}

var services = new ServiceCollection();
services.AddPhotoMindInfrastructure();
var provider = services.BuildServiceProvider();

var paths = provider.GetRequiredService<IAppPaths>();
paths.EnsureCreated();

var factory = provider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>>();
await DatabaseInitializer.InitializeAsync(factory);

var catRepo = provider.GetRequiredService<IClassificationRepository>();
var categories = (await catRepo.GetCategoriesAsync()).ToList();
var topLevel = categories.Where(c => c.ParentId is null).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();

var clip = provider.GetRequiredService<IClipModelService>();
var configStore = provider.GetRequiredService<IAiConfigStore>();
var config = await configStore.LoadAsync();

await clip.EnsureLoadedAsync();
if (clip.State != PhotoMind.Core.Enums.ClipModelState.Ready)
{
    Console.WriteLine("FATAL: CLIP model not ready: " + clip.LastError);
    return 1;
}

await clip.WarmupAsync(categories);

Console.WriteLine($"CLIP device: {clip.DeviceDescription}  (degraded={clip.Degraded}, {clip.DegradeReason ?? ""})");
Console.WriteLine($"temperature={config.ClipTemperature:F4}  threshold={config.ClipConfidenceThreshold:F2}");
Console.WriteLine($"categories(top)={topLevel.Count}");
Console.WriteLine();

var rows = new List<Row>();
foreach (var top in topLevel)
{
    var label = top.Alias ?? top.Name;
    var folder = Path.Combine(evalDir, label);
    if (!Directory.Exists(folder))
        continue;

    foreach (var file in Directory.EnumerateFiles(folder, "*.jpg").Concat(Directory.EnumerateFiles(folder, "*.jpeg")))
    {
        var emb = await clip.EncodeImageAsync(file);
        var match = await clip.MatchAsync(emb, categories, config.ClipTemperature);
        var predictedTop = categories.FirstOrDefault(c => c.Id == match.CategoryId);
        rows.Add(new Row(
            GroundTruth: top.Name,
            FileName: Path.GetFileName(file),
            PredictedTop: predictedTop?.Name ?? match.CategoryName ?? "?",
            PredictedSub: match.SubCategoryName ?? "-",
            Confidence: match.Confidence,
            Correct: match.CategoryId == top.Id));
    }
}

Console.WriteLine($"{"truth",-10} {"file",-26} {"predicted",-10} {"sub",-16} {"conf",8}  route");
foreach (var r in rows)
{
    var route = r.Confidence >= config.ClipConfidenceThreshold ? "CLIP" : "DeepSeek";
    var mark = r.Correct ? "ok " : "XX ";
    Console.WriteLine($"{r.GroundTruth,-10} {r.FileName,-26} {r.PredictedTop,-10} {r.PredictedSub,-16} {r.Confidence,8:F4}  {mark}{route}");
}

Console.WriteLine();
var n = rows.Count;
var correct = rows.Count(r => r.Correct);
var accepted = rows.Where(r => r.Confidence >= config.ClipConfidenceThreshold).ToList();
var misAccepted = accepted.Count(r => !r.Correct);

Console.WriteLine($"=== RESULT (n={n}) ===");
Console.WriteLine($"top-1 accuracy          : {correct}/{n} = {(n == 0 ? 0 : (double)correct / n * 100):F1}%");
Console.WriteLine($"routed to CLIP (>= {config.ClipConfidenceThreshold:F2}) : {accepted.Count}/{n}");
Console.WriteLine($"DeepSeek reduction ratio : {(n == 0 ? 0 : (double)accepted.Count / n * 100):F1}% (images NOT sent to DeepSeek)");
Console.WriteLine($"mis-acceptance at {config.ClipConfidenceThreshold:F2}     : {misAccepted}/{accepted.Count} = {(accepted.Count == 0 ? 0 : (double)misAccepted / accepted.Count * 100):F1}%");
return 0;

internal sealed record Row(string GroundTruth, string FileName, string PredictedTop, string PredictedSub, double Confidence, bool Correct);
