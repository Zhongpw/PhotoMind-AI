using Microsoft.Extensions.DependencyInjection;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Infrastructure;
using PhotoMind.Infrastructure.AI;
using PhotoMind.Infrastructure.Database;

// Honest evaluation of the CLIP local candidate screen for DeepSeek collaboration.
// No DeepSeek API is called. Measures, per Top-K, the candidate hit rate (does the human
// ground-truth top-level category appear in the CLIP top-K candidates?) and the resulting
// system-prompt length (a proxy for input tokens) versus the no-candidate baseline.
//
// Usage: clip-candidate-eval.exe <validationDir>

var dataDir = Environment.GetEnvironmentVariable("PHOTOMIND_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDir))
{
    Console.WriteLine("FATAL: PHOTOMIND_DATA_DIR not set");
    return 1;
}
Directory.CreateDirectory(dataDir);

var evalDir = args.Length > 0 ? args[0] : "validation";
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
await clip.EnsureLoadedAsync();
if (clip.State != ClipModelState.Ready)
{
    Console.WriteLine("FATAL: CLIP model not ready: " + clip.LastError);
    return 1;
}
await clip.WarmupAsync(categories);

Console.WriteLine($"CLIP device: {clip.DeviceDescription}  (degraded={clip.Degraded})");
Console.WriteLine($"categories(top)={topLevel.Count}  total={categories.Count}");
Console.WriteLine();

// ---- Collect ground-truth samples ----
var samples = new List<Sample>();
foreach (var top in topLevel)
{
    var topAlias = top.Alias ?? top.Name;
    var topFolder = Path.Combine(evalDir, topAlias);
    if (!Directory.Exists(topFolder)) continue;
    foreach (var f in EnumerateImages(topFolder)) samples.Add(new Sample(f, top.Id, top.Name));
    foreach (var sub in categories.Where(c => c.ParentId == top.Id))
    {
        var subFolder = Path.Combine(topFolder, sub.Alias ?? sub.Name);
        if (!Directory.Exists(subFolder)) continue;
        foreach (var f in EnumerateImages(subFolder)) samples.Add(new Sample(f, top.Id, top.Name));
    }
}

if (samples.Count == 0)
{
    Console.WriteLine("FATAL: no images found under " + evalDir);
    return 1;
}

// ---- Baseline (A) prompt length (no candidates) ----
var baselineSystem = (string)PromptBuilder.BuildMessages("data:image/jpeg;base64,AAAA", ClassificationMode.Hybrid, categories, AiDetailLevel.Low, null)[0].Content!;
var baselineChars = baselineSystem.Length;
Console.WriteLine($"baseline system prompt (A, no candidates): {baselineChars} chars (~{baselineChars / 4} tokens)");
Console.WriteLine();

// ---- Candidate hit rate per Top-K ----
Console.WriteLine($"{"K",-5} {"hit",-10} {"hit-rate",-10} {"avg-cand",-10} {"prompt-chars",-13} {"prompt-delta"}");
foreach (var k in new[] { 3, 5, 8, 0 })
{
    var hits = 0;
    var totalCandidates = 0;
    long promptChars = 0;
    foreach (var s in samples)
    {
        var emb = await clip.EncodeImageAsync(s.Path);
        var cand = (await clip.GetTopKCandidatesAsync(emb, categories, k)).ToList();
        totalCandidates += cand.Count;
        if (cand.Any(c => (c.ParentId ?? c.CategoryId) == s.TopId)) hits++;
        var sys = (string)PromptBuilder.BuildMessages("data:image/jpeg;base64,AAAA", ClassificationMode.Hybrid, categories, AiDetailLevel.Low, cand)[0].Content!;
        promptChars += sys.Length;
    }

    var n = samples.Count;
    var avgCand = (double)totalCandidates / n;
    var avgPrompt = (double)promptChars / n;
    Console.WriteLine(
        $"{(k == 0 ? "all" : k.ToString()),-5} {hits + "/" + n,-10} {Pct(hits, n),6:F1}%   {avgCand,8:F2}   {avgPrompt,9:F0}   +{(avgPrompt - baselineChars),6:F0} chars");
}

Console.WriteLine();
Console.WriteLine($"n={samples.Count} samples (no DeepSeek API called)");
Console.WriteLine("Note: candidate screen always keeps the full top-level list as fallback (DeepSeek may go outside),");
Console.WriteLine("so input tokens INCREASE slightly vs baseline; the value is sub-category guidance, not input-token cut.");
return 0;

static IEnumerable<string> EnumerateImages(string folder)
{
    foreach (var ext in new[] { "*.jpg", "*.jpeg", "*.png" })
        foreach (var f in Directory.EnumerateFiles(folder, ext))
            yield return f;
}

static double Pct(int num, int den) => den == 0 ? 0 : (double)num / den * 100;

internal sealed record Sample(string Path, long TopId, string TopName);
