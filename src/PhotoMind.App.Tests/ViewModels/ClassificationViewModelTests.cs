using Moq;
using PhotoMind.App.ViewModels;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;

namespace PhotoMind.App.Tests.ViewModels;

public class ClassificationViewModelTests
{
    private readonly Mock<IClassificationService> _service = new();
    private readonly Mock<IClassificationRepository> _repository = new();

    private ClassificationViewModel CreateVm()
    {
        _repository.Setup(r => r.GetUnclassifiedImageIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        _repository.Setup(r => r.GetRecentResultSummariesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ClassificationResultSummary>());
        return new ClassificationViewModel(_service.Object, _repository.Object);
    }

    [Fact]
    public void Constructor_DefaultModeIsAuto()
    {
        var vm = CreateVm();
        Assert.Equal(ClassificationMode.Auto, vm.Mode);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task StartClassify_PassesModeAndRuns()
    {
        var vm = CreateVm();
        vm.Mode = ClassificationMode.Hybrid;
        _service.Setup(s => s.ClassifyAsync(
                It.IsAny<AiClassifyOptions>(),
                It.IsAny<PhotoMind.Core.Threading.PauseToken>(),
                It.IsAny<IProgress<AiClassifyProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClassificationTask { CompletedCount = 2, FailedCount = 0 });

        await vm.StartClassifyCommand.ExecuteAsync(null);

        _service.Verify(s => s.ClassifyAsync(
            It.Is<AiClassifyOptions>(o => o.Mode == ClassificationMode.Hybrid && !o.IncludeAlreadyClassified),
            It.IsAny<PhotoMind.Core.Threading.PauseToken>(),
            It.IsAny<IProgress<AiClassifyProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("完成", vm.StatusText);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task StartClassify_WhenAlreadyRunning_ReturnsImmediately()
    {
        var vm = CreateVm();
        vm.IsRunning = true;

        await vm.StartClassifyCommand.ExecuteAsync(null);

        _service.Verify(s => s.ClassifyAsync(
            It.IsAny<AiClassifyOptions>(),
            It.IsAny<PhotoMind.Core.Threading.PauseToken>(),
            It.IsAny<IProgress<AiClassifyProgress>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartClassify_ServiceThrows_SetsFailureStatus()
    {
        var vm = CreateVm();
        _service.Setup(s => s.ClassifyAsync(
                It.IsAny<AiClassifyOptions>(),
                It.IsAny<PhotoMind.Core.Threading.PauseToken>(),
                It.IsAny<IProgress<AiClassifyProgress>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await vm.StartClassifyCommand.ExecuteAsync(null);

        Assert.Contains("失败", vm.StatusText);
        Assert.False(vm.IsRunning);
    }
}
