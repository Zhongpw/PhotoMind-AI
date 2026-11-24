using Moq;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Core.Threading;
using PhotoMind.Infrastructure.AI;
using PhotoMind.Infrastructure.Services;
using Serilog;

namespace PhotoMind.Tests.Services;

/// <summary>
/// Verifies the Stage-C token-saving routing in <see cref="ClassificationService"/>: a high-confidence
/// local CLIP match is accepted without a DeepSeek request, while low-confidence, failed, or disabled
/// CLIP falls back to DeepSeek.
/// </summary>
public sealed class ClassificationServiceTests : IDisposable
{
    private readonly string _tempFile;

    public ClassificationServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"clip-classify-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(_tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 });
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    private static Image MakeImage(long id = 1) => new()
    {
        Id = id,
        FileName = $"photo{id}.jpg",
        OriginalPath = $"C:\\photos\\photo{id}.jpg",
    };

    private static Category MakeCategory(long id, string name, long? parentId, string alias) => new()
    {
        Id = id,
        Name = name,
        ParentId = parentId,
        Alias = alias,
    };

    private ClassificationService Build(
        bool saveTokenMode,
        ClipMatchResult? clipResult,
        out Mock<IClassificationRepository> repo,
        out FakeDeepSeekClient client,
        out Mock<IClipModelService> clip,
        Exception? clipError = null)
    {
        var categories = new List<Category>
        {
            MakeCategory(1, "动物", null, "animals"),
            MakeCategory(11, "猫", 1, "a cat"),
        };

        repo = new Mock<IClassificationRepository>();
        repo.Setup(r => r.GetCategoriesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);
        repo.Setup(r => r.GetUnclassifiedImageIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<long> { 1 });
        repo.Setup(r => r.GetImagesByIdsAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Image> { MakeImage() });
        repo.Setup(r => r.CreateTaskAsync(It.IsAny<ClassificationTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClassificationTask t, CancellationToken _) => { t.Id = 100; return t; });
        repo.Setup(r => r.UpdateTaskAsync(It.IsAny<ClassificationTask>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveResultAsync(It.IsAny<ClassificationResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.AddApiUsageAsync(It.IsAny<ApiUsage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.AddCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var normalizer = new Mock<IImageNormalizer>();
        normalizer.Setup(n => n.CreatePreviewAsync(It.IsAny<string>(), It.IsAny<AiConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiPreview(_tempFile, "image/jpeg"));

        var configStore = new Mock<IAiConfigStore>();
        configStore.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AiConfig
        {
            AllowUpload = true,
            SaveTokenMode = saveTokenMode,
            ClipConfidenceThreshold = 0.8,
            ClipTemperature = 0.07,
            Model = "test-model",
            Concurrency = 2,
        });

        var secretStore = new Mock<ISecretStore>();
        secretStore.Setup(s => s.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("test-key");

        clip = new Mock<IClipModelService>();
        clip.Setup(c => c.WarmupAsync(It.IsAny<IReadOnlyList<Category>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        clip.Setup(c => c.EncodeImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new float[512]);
        if (clipError != null)
            clip.Setup(c => c.MatchAsync(It.IsAny<float[]>(), It.IsAny<IReadOnlyList<Category>>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).ThrowsAsync(clipError);
        else if (clipResult != null)
            clip.Setup(c => c.MatchAsync(It.IsAny<float[]>(), It.IsAny<IReadOnlyList<Category>>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).ReturnsAsync(clipResult);

        client = new FakeDeepSeekClient();
        var requestManager = new AIRequestManager(client, Mock.Of<ILogger>());
        var svc = new ClassificationService(repo.Object, normalizer.Object, requestManager, configStore.Object, secretStore.Object, clip.Object, Mock.Of<ILogger>());
        return svc;
    }

    private static Task<ClassificationTask> ClassifyAsync(ClassificationService svc) =>
        svc.ClassifyAsync(new AiClassifyOptions { ImageIds = new[] { 1L } }, new PauseToken());

    [Fact]
    public async Task ClassifyAsync_SaveTokenModeOff_UsesDeepSeekOnly()
    {
        var svc = Build(false, null, out var repo, out var client, out var clip);

        var task = await ClassifyAsync(svc);

        Assert.Equal(TaskState.Completed, task.Status);
        Assert.Equal(1, task.CompletedCount);
        Assert.Equal(1, client.CallCount);
        clip.Verify(c => c.WarmupAsync(It.IsAny<IReadOnlyList<Category>>(), It.IsAny<CancellationToken>()), Times.Never);
        clip.Verify(c => c.EncodeImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveResultAsync(It.Is<ClassificationResult>(x => x.Source == ClassificationSource.DeepSeek), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_ClipHighConfidence_AcceptsWithoutDeepSeek()
    {
        var clipResult = new ClipMatchResult { CategoryId = 1, CategoryName = "animals", Confidence = 0.95 };
        var svc = Build(true, clipResult, out var repo, out var client, out var clip);

        var task = await ClassifyAsync(svc);

        Assert.Equal(TaskState.Completed, task.Status);
        Assert.Equal(1, task.CompletedCount);
        Assert.Equal(0, client.CallCount);
        clip.Verify(c => c.EncodeImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveResultAsync(It.Is<ClassificationResult>(x =>
            x.Source == ClassificationSource.Clip && x.CategoryId == 1 && x.Description == "动物" && x.ModelName == "clip-vit-base-patch32"), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddApiUsageAsync(It.IsAny<ApiUsage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClassifyAsync_ClipLowConfidence_FallsBackToDeepSeek()
    {
        var clipResult = new ClipMatchResult { CategoryId = 1, CategoryName = "animals", Confidence = 0.5 };
        var svc = Build(true, clipResult, out var repo, out var client, out var clip);

        var task = await ClassifyAsync(svc);

        Assert.Equal(TaskState.Completed, task.Status);
        Assert.Equal(1, task.CompletedCount);
        Assert.Equal(1, client.CallCount);
        repo.Verify(r => r.SaveResultAsync(It.Is<ClassificationResult>(x => x.Source == ClassificationSource.DeepSeek), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddApiUsageAsync(It.IsAny<ApiUsage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_ClipFailure_FallsBackToDeepSeek()
    {
        var svc = Build(true, null, out var repo, out var client, out var clip, clipError: new InvalidOperationException("CLIP boom"));

        var task = await ClassifyAsync(svc);

        Assert.Equal(TaskState.Completed, task.Status);
        Assert.Equal(1, task.CompletedCount);
        Assert.Equal(1, client.CallCount);
        repo.Verify(r => r.SaveResultAsync(It.Is<ClassificationResult>(x => x.Source == ClassificationSource.DeepSeek), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class FakeDeepSeekClient : IDeepSeekClient
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<DeepSeekResponse> ChatAsync(string baseUrl, string apiKey, DeepSeekRequest request, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            var response = new DeepSeekResponse
            {
                Choices = new List<DeepSeekChoice>
                {
                    new()
                    {
                        Message = new DeepSeekResponseMessage
                        {
                            Content = "{\"category\":\"动物\",\"subcategory\":\"猫\",\"description\":\"一只猫\",\"reason\":\"测试\",\"confidence\":0.9,\"needs_review\":false,\"tags\":[\"猫\"]}",
                        },
                    },
                },
                Usage = new DeepSeekUsage { PromptTokens = 10, CompletionTokens = 20, TotalTokens = 30 },
            };
            return Task.FromResult(response);
        }
    }
}
