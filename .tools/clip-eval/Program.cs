using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.Database;

// Honest, independent evaluation of the local CLIP zero-shot router + confidence calibration.
//
// Usage:
//   clip-eval.exe <validationDir> [--csv <path>] [--val-frac 0.4] [--seed 12345]
//
// The validation dir is a tree of real photos: <topAlias>/<subAlias>/NN.jpg (sub-category images)
// or <topAlias>/NN.jpg (top-level images). The folder names are the categories' English aliases and
// are the human ground-truth labels. No DeepSeek API is called; the tool only runs local CLIP.

var dataDir = Environment.GetEnvironmentVariable("PHOTOMIND_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDir))
{
    Console.WriteLine("FATAL: PHOTOMIND_DATA_DIR not set");
    return 1;
}
Directory.CreateDirectory(dataDir);

var evalDir = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : "validation";
var csvPath = GetArg(args, "--csv");
var valFrac = double.TryParse(GetArg(args, "--val-frac"), NumberStyles.Float, CultureInfo.InvariantCulture, out var vf) ? vf : 0.4;
var seed = int.TryParse(GetArg(args, "--seed"), out var seedParsed) ? seedParsed : 12345;

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
var config = (await configStore.LoadAsync()).Sanitized();

await clip.EnsureLoadedAsync();
if (clip.State != PhotoMind.Core.Enums.ClipModelState.Ready)
{
    Console.WriteLine("FATAL: CLIP model not ready: " + clip.LastError);
    return 1;
}

await clip.WarmupAsync(categories);

Console.WriteLine($"CLIP device: {clip.DeviceDescription}  (degraded={clip.Degraded}, {clip.DegradeReason ?? ""})");
Console.WriteLine($"temperature={config.ClipTemperature:F4}  threshold={config.ClipConfidenceThreshold:F2}  calSlope={config.ClipCalibrationSlope}  calIntercept={config.ClipCalibrationIntercept}");
Console.WriteLine($"categories(top)={topLevel.Count}");
Console.WriteLine();

// ---- 1. Collect ground-truth labelled samples from the folder tree ----
var samples = new List<Sample>();
foreach (var top in topLevel)
{
    var topAlias = top.Alias ?? top.Name;
    var topFolder = Path.Combine(evalDir, topAlias);
    if (!Directory.Exists(topFolder))
        continue;

    // Top-level images directly in <topAlias>/.
    foreach (var file in EnumerateImages(topFolder))
        samples.Add(new Sample(file, top.Id, top.Name, null, null));

    // Sub-category images in <topAlias>/<subAlias>/.
    foreach (var sub in categories.Where(c => c.ParentId == top.Id))
    {
        var subAlias = sub.Alias ?? sub.Name;
        var subFolder = Path.Combine(topFolder, subAlias);
        if (!Directory.Exists(subFolder))
            continue;
        foreach (var file in EnumerateImages(subFolder))
            samples.Add(new Sample(file, top.Id, top.Name, sub.Id, sub.Name));
    }
}

if (samples.Count == 0)
{
    Console.WriteLine("FATAL: no images found under " + evalDir);
    return 1;
}

// ---- 2. Run CLIP inference on every sample ----
var rows = new List<Row>();
foreach (var sample in samples)
{
    var emb = await clip.EncodeImageAsync(sample.Path);
    var match = await clip.MatchAsync(emb, categories, config.ClipTemperature);
    var predictedTop = categories.FirstOrDefault(c => c.Id == match.CategoryId);
    var predictedSub = match.SubCategoryId is long sid ? categories.FirstOrDefault(c => c.Id == sid) : null;
    var rawConf = match.Confidence;
    var calConf = ClipCalibration.Calibrate(match.Margin, config.ClipCalibrationSlope, config.ClipCalibrationIntercept);

    rows.Add(new Row(
        GroundTruthTop: sample.TopName,
        GroundTruthSub: sample.SubName,
        FileName: sample.Path,
        PredictedTop: predictedTop?.Name ?? match.CategoryName ?? "?",
        PredictedSub: predictedSub?.Name ?? "-",
        Top1Similarity: match.Top1Similarity,
        Top2Similarity: match.Top2Similarity,
        Margin: match.Margin,
        RawConfidence: rawConf,
        CalibratedConfidence: calConf,
        CorrectTop: match.CategoryId == sample.TopId,
        CorrectSub: sample.SubId != null && match.SubCategoryId == sample.SubId));
}

// ---- 3. Export CSV ----
if (!string.IsNullOrWhiteSpace(csvPath))
    ExportCsv(csvPath, rows);

// ---- 4. Console table + overall accuracy ----
Console.WriteLine($"{"truth",-12} {"sub",-12} {"pred",-10} {"psub",-14} {"top1sim",8} {"margin",8} {"raw",7} {"cal",7}  ok");
foreach (var r in rows)
{
    var mark = r.CorrectTop ? "ok " : "XX ";
    var calText = double.IsNaN(r.CalibratedConfidence) ? "-" : r.CalibratedConfidence.ToString("F4");
    Console.WriteLine(
        $"{r.GroundTruthTop,-12} {r.GroundTruthSub ?? "-",-12} {r.PredictedTop,-10} {r.PredictedSub,-14} " +
        $"{r.Top1Similarity,8:F4} {r.Margin,8:F4} {r.RawConfidence,7:F4} {calText,7}  {mark}");
}

Console.WriteLine();
var n = rows.Count;
var correctTop = rows.Count(r => r.CorrectTop);
var correctSub = rows.Count(r => r.CorrectSub && r.GroundTruthSub != null);
var subN = rows.Count(r => r.GroundTruthSub != null);

Console.WriteLine($"=== OVERALL (n={n}) ===");
Console.WriteLine($"top-1 accuracy (top-level) : {correctTop}/{n} = {Pct(correctTop, n):F1}%");
Console.WriteLine($"top-1 accuracy (sub-level)  : {correctSub}/{subN} = {Pct(correctSub, subN):F1}%");
var maxRaw = rows.Max(r => r.RawConfidence);
Console.WriteLine($"raw softmax conf  max/mean  : {maxRaw:F4} / {rows.Average(r => r.RawConfidence):F4}");
Console.WriteLine();

// Per-category accuracy (top-level).
Console.WriteLine("=== PER-CATEGORY (top-level) ===");
foreach (var g in rows.GroupBy(r => r.GroundTruthTop).OrderBy(g => g.Key))
{
    var c = g.Count(r => r.CorrectTop);
    Console.WriteLine($"  {g.Key,-14} {c}/{g.Count()} = {Pct(c, g.Count()):F1}%");
}
Console.WriteLine();

// ---- 5. Calibration: fit logistic on a held-out split, evaluate on the rest ----
var (calSet, valSet) = StratifiedSplit(rows, valFrac, seed);
Console.WriteLine($"=== CALIBRATION (fit on {calSet.Count}, evaluate on {valSet.Count}) ===");

// (a) Margin-based calibration: calibrated = sigmoid(slope * margin + intercept).
var (mSlope, mIntercept) = FitLogistic(calSet, r => r.Margin);
Console.WriteLine($"[margin]   fitted: slope={mSlope:F4}  intercept={mIntercept:F4}  ->  calibrated = sigmoid({mSlope:F2}*margin + {mIntercept:F2})");

// (b) Raw-similarity (Platt/temperature) calibration: calibrated = sigmoid(slope * top1sim + intercept).
var (sSlope, sIntercept) = FitLogistic(calSet, r => r.Top1Similarity);
Console.WriteLine($"[top1sim]  fitted: slope={sSlope:F4}  intercept={sIntercept:F4}  ->  calibrated = sigmoid({sSlope:F2}*top1sim + {sIntercept:F2})");
Console.WriteLine($"  (fit uses one feature at a time; a larger, balanced, human-labelled set is required for a reliable calibration)");
Console.WriteLine();

foreach (var (label, set) in new[] { ("calibration set (fit)", calSet), ("validation set (held-out)", valSet) })
{
    Console.WriteLine($"  --- {label} (n={set.Count}) ---");
    ReportCalibration("margin", set, set.Select(r => ClipCalibration.Calibrate(r.Margin, mSlope, mIntercept)).ToArray(), config.ClipConfidenceThreshold);
    ReportCalibration("top1sim", set, set.Select(r => ClipCalibration.Calibrate(r.Top1Similarity, sSlope, sIntercept)).ToArray(), config.ClipConfidenceThreshold);
    Console.WriteLine();
}

return 0;

// ---------------------------------------------------------------------------

static void ReportCalibration(string name, List<Row> set, double[] calibrated, double threshold)
{
    var acc = set.Count(r => r.CorrectTop);

    var acceptedCount = 0;
    var misCount = 0;
    for (var i = 0; i < set.Count; i++)
    {
        if (!double.IsNaN(calibrated[i]) && calibrated[i] >= threshold)
        {
            acceptedCount++;
            if (!set[i].CorrectTop)
                misCount++;
        }
    }

    Console.WriteLine($"  [{name,-8}] accepted@{threshold:F2} = {acceptedCount}/{set.Count} ({Pct(acceptedCount, set.Count):F1}% coverage), mis-accept {misCount}/{acceptedCount} = {Pct(misCount, acceptedCount):F1}%");
}

static (double slope, double intercept) FitLogistic(List<Row> rows, Func<Row, double> feature)
{
    var xs = rows.Select(feature).ToArray();
    var labels = rows.Select(r => r.CorrectTop ? 1.0 : 0.0).ToArray();
    var m = xs.Length;

    double slope = 0, intercept = 0;
    const int iterations = 5000;
    const double lr = 0.5;
    const double l2 = 0.001;

    for (var it = 0; it < iterations; it++)
    {
        double gSlope = 0, gIntercept = 0;
        for (var i = 0; i < m; i++)
        {
            var p = ClipCalibration.Sigmoid(slope * xs[i] + intercept);
            var diff = p - labels[i];
            gSlope += diff * xs[i];
            gIntercept += diff;
        }
        gSlope = gSlope / m + l2 * slope;
        gIntercept /= m;
        slope -= lr * gSlope;
        intercept -= lr * gIntercept;
    }

    return (slope, intercept);
}

static (List<Row> cal, List<Row> val) StratifiedSplit(List<Row> rows, double valFrac, int seed)
{
    // Round-robin within each top-level category so the split stays balanced and near-duplicate
    // images from the same search term (same sub-folder) never land together.
    var rng = new Random(seed);
    var cal = new List<Row>();
    var val = new List<Row>();

    foreach (var group in rows.GroupBy(r => r.GroundTruthTop))
    {
        var shuffled = group.OrderBy(_ => rng.Next()).ToList();
        var valCount = Math.Max(0, (int)Math.Round(shuffled.Count * valFrac));
        for (var i = 0; i < shuffled.Count; i++)
        {
            if (i < valCount) val.Add(shuffled[i]);
            else cal.Add(shuffled[i]);
        }
    }

    // Ensure both sets are non-empty.
    if (cal.Count == 0) { cal.Add(val[0]); val.RemoveAt(0); }
    if (val.Count == 0) { val.Add(cal[0]); cal.RemoveAt(0); }
    return (cal, val);
}

static IEnumerable<string> EnumerateImages(string folder)
{
    foreach (var ext in new[] { "*.jpg", "*.jpeg", "*.png" })
        foreach (var f in Directory.EnumerateFiles(folder, ext))
            yield return f;
}

static void ExportCsv(string path, List<Row> rows)
{
    var sb = new StringBuilder();
    sb.AppendLine("ground_truth_top,ground_truth_sub,predicted_top,predicted_sub,top1_similarity,top2_similarity,margin,raw_confidence,calibrated_confidence,correct_top,correct_sub,file");
    foreach (var r in rows)
        sb.AppendLine(string.Join(',',
            Csv(r.GroundTruthTop), Csv(r.GroundTruthSub ?? ""), Csv(r.PredictedTop), Csv(r.PredictedSub),
            r.Top1Similarity.ToString("F6", CultureInfo.InvariantCulture),
            r.Top2Similarity.ToString("F6", CultureInfo.InvariantCulture),
            r.Margin.ToString("F6", CultureInfo.InvariantCulture),
            r.RawConfidence.ToString("F6", CultureInfo.InvariantCulture),
            double.IsNaN(r.CalibratedConfidence) ? "" : r.CalibratedConfidence.ToString("F6", CultureInfo.InvariantCulture),
            r.CorrectTop ? "1" : "0", r.CorrectSub ? "1" : "0",
            Csv(r.FileName)));
    File.WriteAllText(path, sb.ToString());
    Console.WriteLine($"CSV written: {path} ({rows.Count} rows)");
    Console.WriteLine();
}

static string Csv(string v) => v.Contains(',') || v.Contains('"') ? '"' + v.Replace("\"", "\"\"") + '"' : v;

static double Pct(int num, int den) => den == 0 ? 0 : (double)num / den * 100;

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == name)
            return args[i + 1];
    return null;
}

internal sealed record Sample(string Path, long TopId, string TopName, long? SubId, string? SubName);

internal sealed record Row(
    string GroundTruthTop,
    string? GroundTruthSub,
    string FileName,
    string PredictedTop,
    string PredictedSub,
    double Top1Similarity,
    double Top2Similarity,
    double Margin,
    double RawConfidence,
    double CalibratedConfidence,
    bool CorrectTop,
    bool CorrectSub);
