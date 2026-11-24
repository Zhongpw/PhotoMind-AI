using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using Serilog;

namespace PhotoMind.Infrastructure.AI;

/// <summary>
/// ONNX Runtime CLIP (ViT-B/32) inference. Loads vision_model.onnx + text_model.onnx once, encodes
/// images and text into L2-normalized embeddings, and matches images against the category tree using
/// temperature softmax. Runs on CUDA when the GPU acceleration toggle is enabled and an NVIDIA GPU is
/// available; otherwise it transparently falls back to CPU.
/// </summary>
public sealed class ClipModelService : IClipModelService
{
    private const string PromptTemplate = "a photo of {0}";

    private readonly IAppPaths _paths;
    private readonly IImageFormatDetector _detector;
    private readonly IAiConfigStore _configStore;
    private readonly ILogger _logger;
    private readonly ClipTextCache _textCache;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly object _sessionLock = new();

    private InferenceSession? _visionSession;
    private InferenceSession? _textSession;
    private ClipTokenizer? _tokenizer;
    private string _visionInputName = string.Empty;
    private string _textInputName = string.Empty;
    private string _visionOutputName = string.Empty;
    private string _textOutputName = string.Empty;

    public ClipModelState State { get; private set; } = ClipModelState.NotLoaded;
    public InferenceDevice Device { get; private set; } = InferenceDevice.Cpu;
    public string DeviceDescription { get; private set; } = "CPU";
    public string? LastError { get; private set; }
    public bool Degraded { get; private set; }
    public string? DegradeReason { get; private set; }

    public ClipModelService(IAppPaths paths, IImageFormatDetector detector, IAiConfigStore configStore, ILogger logger)
    {
        _paths = paths;
        _detector = detector;
        _configStore = configStore;
        _logger = logger;
        _textCache = new ClipTextCache(paths.ClipTextCacheFilePath);
    }

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (State == ClipModelState.Ready)
            return;

        await _loadLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (State == ClipModelState.Ready)
                return;

            State = ClipModelState.Loading;

            var dir = _paths.ClipModelDirectory;
            var visionPath = Path.Combine(dir, "vision_model.onnx");
            var textPath = Path.Combine(dir, "text_model.onnx");
            var vocabPath = Path.Combine(dir, "vocab.json");
            var mergesPath = Path.Combine(dir, "merges.txt");

            if (!File.Exists(visionPath) || !File.Exists(textPath))
                throw new PhotoMindConfigurationException("CLIP 模型文件缺失（vision_model.onnx / text_model.onnx）。请先下载模型到模型目录。");
            if (!File.Exists(vocabPath) || !File.Exists(mergesPath))
                throw new PhotoMindConfigurationException("CLIP 分词器文件缺失（vocab.json / merges.txt）。");

            var config = await _configStore.LoadAsync(ct).ConfigureAwait(false);

            // Build session options, attempting CUDA first when the user enabled GPU acceleration.
            var options = BuildSessionOptions(config.GpuAcceleration);
            try
            {
                lock (_sessionLock)
                {
                    _visionSession?.Dispose();
                    _textSession?.Dispose();
                    _visionSession = new InferenceSession(visionPath, options);
                    _textSession = new InferenceSession(textPath, options);
                }
            }
            catch (Exception ex) when (Device == InferenceDevice.Cuda)
            {
                // CUDA registered but session creation failed (e.g. missing cuDNN/CUDA runtime).
                // Retry with a CPU-only configuration.
                _logger.Warning(ex, "CUDA session creation failed, retrying CPU-only");
                Degraded = true;
                DegradeReason = "GPU 加速失败（" + ex.Message + "），已回退 CPU";
                Device = InferenceDevice.Cpu;
                DeviceDescription = "CPU";
                options = BuildCpuOptions();
                lock (_sessionLock)
                {
                    _visionSession?.Dispose();
                    _textSession?.Dispose();
                    _visionSession = new InferenceSession(visionPath, options);
                    _textSession = new InferenceSession(textPath, options);
                }
            }

            _tokenizer = await ClipTokenizer.LoadAsync(vocabPath, mergesPath, ct).ConfigureAwait(false);

            _visionInputName = _visionSession.InputMetadata.Keys.FirstOrDefault()
                               ?? throw new PhotoMindConfigurationException("CLIP 视觉模型没有输入。");
            _visionOutputName = _visionSession.OutputMetadata.Keys.FirstOrDefault()
                                ?? throw new PhotoMindConfigurationException("CLIP 视觉模型没有输出。");
            _textInputName = _textSession.InputMetadata.Keys.FirstOrDefault()
                             ?? throw new PhotoMindConfigurationException("CLIP 文本模型没有输入。");
            _textOutputName = _textSession.OutputMetadata.Keys.FirstOrDefault()
                              ?? throw new PhotoMindConfigurationException("CLIP 文本模型没有输出。");

            State = ClipModelState.Ready;
            LastError = null;
            _logger.Information("CLIP model loaded. device={Dev} inputVision={Vi} inputText={Ti}",
                DeviceDescription, _visionInputName, _textInputName);
        }
        catch (Exception ex)
        {
            State = ClipModelState.Failed;
            LastError = ex.Message;
            _logger.Error(ex, "CLIP model load failed");
            throw new PhotoMindConfigurationException("CLIP 模型加载失败：" + ex.Message, ex);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<float[]> EncodeImageAsync(string imagePath, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        var format = _detector.Detect(ReadHeader(imagePath));
        var input = ClipImagePreprocessor.Preprocess(imagePath, format);
        var tensor = new DenseTensor<float>(input, new[] { 1, 3, AiConstants.ClipImageSize, AiConstants.ClipImageSize });

        InferenceSession session;
        string inputName, outputName;
        lock (_sessionLock)
        {
            session = _visionSession!;
            inputName = _visionInputName;
            outputName = _visionOutputName;
        }

        var embedding = await Task.Run(() => RunEmbedding(session, inputName, outputName, tensor), ct).ConfigureAwait(false);
        return Normalize(embedding);
    }

    public async Task<float[]> EncodeTextAsync(string text, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        var tokenizer = _tokenizer!;
        var inputIds = tokenizer.Tokenize(text).Select(i => (long)i).ToArray();
        var tensor = new DenseTensor<long>(inputIds, new[] { 1, AiConstants.ClipTextSequenceLength });

        InferenceSession session;
        string inputName, outputName;
        lock (_sessionLock)
        {
            session = _textSession!;
            inputName = _textInputName;
            outputName = _textOutputName;
        }

        var embedding = await Task.Run(() => RunEmbedding(session, inputName, outputName, tensor), ct).ConfigureAwait(false);
        return Normalize(embedding);
    }

    public async Task WarmupAsync(IReadOnlyList<Category> categories, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);
        foreach (var category in categories)
            await GetCategoryEmbeddingAsync(category, ct).ConfigureAwait(false);
        _textCache.Save();
    }

    public async Task<ClipMatchResult> MatchAsync(
        float[] imageEmbedding,
        IReadOnlyList<Category> categories,
        double temperature,
        CancellationToken ct = default)
    {
        var topLevel = categories.Where(c => c.ParentId is null).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
        if (topLevel.Count == 0)
            return new ClipMatchResult { Confidence = 0 };

        // 1. Match against top-level categories.
        var topEmbeddings = new float[topLevel.Count][];
        for (var i = 0; i < topLevel.Count; i++)
            topEmbeddings[i] = await GetCategoryEmbeddingAsync(topLevel[i], ct).ConfigureAwait(false);

        var topSims = topEmbeddings.Select(e => (double)Dot(imageEmbedding, e)).ToArray();
        var topProbs = Softmax(topSims, temperature);

        var topIdx = ArgMax(topProbs);
        var topCategory = topLevel[topIdx];

        // Raw similarity margin between top-1 and top-2: the calibration signal.
        var top1Sim = topSims[topIdx];
        var top2Sim = topLevel.Count > 1
            ? topSims.Where((_, i) => i != topIdx).Max()
            : top1Sim;
        var margin = top1Sim - top2Sim;

        // 2. Match against that top-level's children.
        var children = categories.Where(c => c.ParentId == topCategory.Id).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
        var subCategoryId = (long?)null;
        var subCategoryName = string.Empty;
        var subConfidence = 0d;

        if (children.Count > 0)
        {
            var childEmbeddings = new float[children.Count][];
            for (var i = 0; i < children.Count; i++)
                childEmbeddings[i] = await GetCategoryEmbeddingAsync(children[i], ct).ConfigureAwait(false);

            var childSims = childEmbeddings.Select(e => (double)Dot(imageEmbedding, e)).ToArray();
            var childProbs = Softmax(childSims, temperature);

            var childIdx = ArgMax(childProbs);
            subCategoryId = children[childIdx].Id;
            subCategoryName = children[childIdx].Alias ?? children[childIdx].Name;
            subConfidence = childProbs[childIdx];
        }

        return new ClipMatchResult
        {
            CategoryId = topCategory.Id,
            SubCategoryId = subCategoryId,
            CategoryName = topCategory.Alias ?? topCategory.Name,
            SubCategoryName = subCategoryName,
            Confidence = topProbs[topIdx],
            SubCategoryConfidence = subConfidence,
            Top1Similarity = top1Sim,
            Top2Similarity = top2Sim,
            Margin = margin,
            ImageEmbedding = imageEmbedding,
        };
    }

    public async Task<IReadOnlyList<ClipCandidate>> GetTopKCandidatesAsync(
        float[] imageEmbedding,
        IReadOnlyList<Category> categories,
        int topK,
        CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        if (categories.Count == 0)
            return Array.Empty<ClipCandidate>();

        // Rank every category (top-level and sub-category) by raw cosine similarity to the image.
        // This is a screening signal for DeepSeek, not a calibrated probability.
        var byId = categories.ToDictionary(c => c.Id);
        var scored = new List<ClipCandidate>(categories.Count);
        foreach (var category in categories)
        {
            var embedding = await GetCategoryEmbeddingAsync(category, ct).ConfigureAwait(false);
            var parent = category.ParentId is long pid && byId.TryGetValue(pid, out var p) ? p : null;
            scored.Add(new ClipCandidate
            {
                CategoryId = category.Id,
                Name = category.Name,
                ParentId = category.ParentId,
                ParentName = parent?.Name,
                Similarity = Dot(imageEmbedding, embedding),
            });
        }

        var ordered = scored.OrderByDescending(c => c.Similarity).ToList();
        if (topK > 0 && topK < ordered.Count)
            ordered = ordered.Take(topK).ToList();
        return ordered;
    }

    private async Task<float[]> GetCategoryEmbeddingAsync(Category category, CancellationToken ct)
    {
        var prompts = GetPrompts(category);

        // The cache key embeds the full prompt set so a classification-system change naturally
        // invalidates stale entries (old entries are simply orphaned, never served for new prompts).
        var label = string.Join("\u0001", prompts);
        var key = ClipTextCache.MakeKey(category.Id, label);

        if (_textCache.TryGet(key, out var cached) && cached is { Length: > 0 })
            return cached;

        // Prompt ensemble: mean (spherical mean after re-normalization) of the per-prompt embeddings.
        var sum = new float[AiConstants.ClipEmbeddingDim];
        foreach (var prompt in prompts)
        {
            var text = string.Format(PromptTemplate, prompt);
            var embedding = await EncodeTextAsync(text, ct).ConfigureAwait(false);
            for (var i = 0; i < sum.Length; i++)
                sum[i] += embedding[i];
        }

        var mean = Normalize(sum);
        _textCache.Set(key, mean);
        return mean;
    }

    /// <summary>
    /// Returns the CLIP prompt(s) for a category: the <see cref="Category.PromptsJson"/> ensemble when
    /// present, otherwise the single <see cref="Category.Alias"/> (falling back to the Chinese name).
    /// </summary>
    private static string[] GetPrompts(Category category)
    {
        if (!string.IsNullOrWhiteSpace(category.PromptsJson))
        {
            try
            {
                var prompts = JsonSerializer.Deserialize<string[]>(category.PromptsJson);
                var trimmed = prompts?
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToArray();
                if (trimmed is { Length: > 0 })
                    return trimmed;
            }
            catch (JsonException)
            {
                // Malformed PromptsJson: fall through to the single-alias path.
            }
        }

        var alias = string.IsNullOrWhiteSpace(category.Alias) ? category.Name : category.Alias;
        return new[] { alias };
    }

    /// <summary>Builds session options; attempts CUDA when requested, falling back to CPU.</summary>
    private SessionOptions BuildSessionOptions(bool gpuAcceleration)
    {
        var options = new SessionOptions();

        if (gpuAcceleration)
        {
            try
            {
                options.AppendExecutionProvider_CUDA(0);
                Device = InferenceDevice.Cuda;
                DeviceDescription = "NVIDIA CUDA (GPU)";
                Degraded = false;
                DegradeReason = null;
                return options;
            }
            catch (Exception ex)
            {
                // CUDA provider unavailable: no NVIDIA GPU, no CUDA runtime, or not in this build.
                _logger.Warning(ex, "CUDA execution provider unavailable, falling back to CPU");
                Degraded = true;
                DegradeReason = "GPU 加速不可用（" + ex.Message + "），已回退 CPU";
                options = new SessionOptions();
            }
        }

        options.AppendExecutionProvider_CPU(0);
        Device = InferenceDevice.Cpu;
        DeviceDescription = "CPU";
        if (!gpuAcceleration)
        {
            Degraded = false;
            DegradeReason = null;
        }
        return options;
    }

    private static SessionOptions BuildCpuOptions()
    {
        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU(0);
        return options;
    }

    private static float[] RunEmbedding(InferenceSession session, string inputName, string outputName, Tensor<float> tensor)
    {
        using var outputs = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) });
        var output = outputs.First(r => r.Name == outputName);
        return output.AsTensor<float>().ToArray();
    }

    private static float[] RunEmbedding(InferenceSession session, string inputName, string outputName, Tensor<long> tensor)
    {
        using var outputs = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) });
        var output = outputs.First(r => r.Name == outputName);
        return output.AsTensor<float>().ToArray();
    }

    private static byte[] ReadHeader(string path)
    {
        using var fs = File.OpenRead(path);
        var buffer = new byte[16];
        var read = fs.Read(buffer, 0, buffer.Length);
        if (read < 16)
            Array.Resize(ref buffer, read);
        return buffer;
    }

    private static float[] Normalize(float[] v)
    {
        var norm = 0f;
        foreach (var x in v)
            norm += x * x;
        norm = MathF.Sqrt(norm);

        if (norm < 1e-12f)
            return v;

        var result = new float[v.Length];
        for (var i = 0; i < v.Length; i++)
            result[i] = v[i] / norm;
        return result;
    }

    private static float Dot(float[] a, float[] b)
    {
        var sum = 0f;
        for (var i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }

    private static double[] Softmax(double[] logits, double temperature)
    {
        var scaled = new double[logits.Length];
        var max = double.NegativeInfinity;
        for (var i = 0; i < logits.Length; i++)
        {
            scaled[i] = logits[i] / temperature;
            if (scaled[i] > max)
                max = scaled[i];
        }

        var sum = 0d;
        for (var i = 0; i < scaled.Length; i++)
        {
            scaled[i] = Math.Exp(scaled[i] - max);
            sum += scaled[i];
        }

        for (var i = 0; i < scaled.Length; i++)
            scaled[i] /= sum;
        return scaled;
    }

    private static int ArgMax(double[] values)
    {
        var best = 0;
        for (var i = 1; i < values.Length; i++)
            if (values[i] > values[best])
                best = i;
        return best;
    }
}
