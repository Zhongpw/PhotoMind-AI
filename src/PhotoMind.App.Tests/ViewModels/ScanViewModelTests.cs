using Moq;
using PhotoMind.App.ViewModels;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Interfaces;
using PhotoMind.Core.Models;
using PhotoMind.Infrastructure.FileSystem;

namespace PhotoMind.App.Tests.ViewModels;

public class ScanViewModelTests
{
    private readonly Mock<IScanService> _scanService = new();
    private readonly Mock<IScanRepository> _repository = new();

    private ScanViewModel CreateVm()
    {
        var detector = Mock.Of<IImageFormatDetector>();
        var scanner = new DirectoryScanner(detector, new FakePaths());
        return new ScanViewModel(_scanService.Object, _repository.Object, scanner);
    }

    private static ImageItemViewModel Item(long id) =>
        new(new Image
        {
            Id = id,
            FileName = $"photo{id}.jpg",
            OriginalPath = $"C:\\photos\\photo{id}.jpg",
            FileSize = 1024,
            Width = 32,
            Height = 32,
            ScanStatus = ImageScanStatus.Scanned,
        });

    [Fact]
    public async Task RemoveFromTaskCore_SingleRemovesFromListAndPersists()
    {
        var vm = CreateVm();
        vm.Images.Add(Item(1));
        vm.Images.Add(Item(2));
        vm.Images.Add(Item(3));
        _repository.Setup(r => r.MarkImagesSkippedAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var removed = await vm.RemoveFromTaskCoreAsync(new[] { 2L });

        Assert.Equal(1, removed);
        Assert.Equal(2, vm.Images.Count);
        Assert.DoesNotContain(vm.Images, i => i.Id == 2);
        _repository.Verify(r => r.MarkImagesSkippedAsync(
            It.Is<IReadOnlyList<long>>(ids => ids.SequenceEqual(new[] { 2L })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveFromTaskCore_BatchRemovesMultiple()
    {
        var vm = CreateVm();
        vm.Images.Add(Item(1));
        vm.Images.Add(Item(2));
        vm.Images.Add(Item(3));
        vm.Images.Add(Item(4));
        _repository.Setup(r => r.MarkImagesSkippedAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var removed = await vm.RemoveFromTaskCoreAsync(new[] { 1L, 3L });

        Assert.Equal(2, removed);
        Assert.Equal(2, vm.Images.Count);
        Assert.DoesNotContain(vm.Images, i => i.Id == 1);
        Assert.DoesNotContain(vm.Images, i => i.Id == 3);
        Assert.Contains(vm.Images, i => i.Id == 2);
        Assert.Contains(vm.Images, i => i.Id == 4);
    }

    [Fact]
    public async Task RemoveFromTaskCore_EmptyList_ReturnsZeroWithoutRepositoryCall()
    {
        var vm = CreateVm();

        var removed = await vm.RemoveFromTaskCoreAsync(Array.Empty<long>());

        Assert.Equal(0, removed);
        _repository.Verify(r => r.MarkImagesSkippedAsync(
            It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveFromTaskCore_WhenRepositoryChangesNothing_LeavesListUnchanged()
    {
        var vm = CreateVm();
        vm.Images.Add(Item(1));
        vm.Images.Add(Item(2));
        _repository.Setup(r => r.MarkImagesSkippedAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // already skipped server-side

        var removed = await vm.RemoveFromTaskCoreAsync(new[] { 1L });

        Assert.Equal(0, removed);
        Assert.Equal(2, vm.Images.Count); // UI must NOT remove items whose state was not persisted
    }

    private sealed class FakePaths : IAppPaths
    {
        public string DataDirectory => "C:\\fake\\data";
        public string DatabasePath => "C:\\fake\\data\\photomin.db";
        public string ThumbnailDirectory => "C:\\fake\\data\\thumbnails";
        public string LogDirectory => "C:\\fake\\data\\logs";
        public string TempDirectory => "C:\\fake\\data\\temp";
        public string ConfigFilePath => "C:\\fake\\data\\ai-config.json";
        public string SecretsFilePath => "C:\\fake\\data\\secrets.dat";
        public void EnsureCreated() { }
    }
}
