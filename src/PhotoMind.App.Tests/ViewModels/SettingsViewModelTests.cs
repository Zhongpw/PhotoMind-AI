using Moq;
using PhotoMind.App.ViewModels;
using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.App.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<ISecretStore> _secretStore = new();
    private readonly Mock<IAiConfigStore> _configStore = new();
    private readonly Mock<IDeepSeekClient> _client = new();

    private SettingsViewModel CreateVm()
    {
        _secretStore.Setup(s => s.HasSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _configStore.Setup(c => c.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiConfig());
        return new SettingsViewModel(_secretStore.Object, _configStore.Object, _client.Object);
    }

    [Fact]
    public async Task SaveApiKey_EmptyInput_ShowsPromptAndDoesNotSave()
    {
        var vm = CreateVm();
        vm.ApiKeyInput = "   ";

        await vm.SaveApiKeyCommand.ExecuteAsync(null);

        Assert.Equal("请先输入 API Key", vm.StatusText);
        _secretStore.Verify(s => s.SaveSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveApiKey_ValidInput_SavesTrimmedKey()
    {
        var vm = CreateVm();
        vm.ApiKeyInput = "  sk-test-123  ";

        await vm.SaveApiKeyCommand.ExecuteAsync(null);

        _secretStore.Verify(s => s.SaveSecretAsync(AiConstants.ApiKeySecretName, "sk-test-123", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(vm.HasApiKey);
        Assert.Equal(string.Empty, vm.ApiKeyInput);
    }

    [Fact]
    public async Task DeleteApiKey_DeletesAndClearsFlag()
    {
        var vm = CreateVm();
        vm.HasApiKey = true;

        await vm.DeleteApiKeyCommand.ExecuteAsync(null);

        _secretStore.Verify(s => s.DeleteSecretAsync(AiConstants.ApiKeySecretName, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(vm.HasApiKey);
    }

    [Fact]
    public async Task SaveConfig_PersistsCurrentValues()
    {
        var vm = CreateVm();
        vm.BaseUrl = "https://api.deepseek.com";
        vm.Model = "deepseek-flash";
        vm.Detail = AiDetailLevel.Original;
        vm.Concurrency = 4;
        vm.MaxRetries = 5;
        vm.AllowUpload = true;
        vm.MaxPreviewDimension = 1536;
        vm.PreviewQuality = 90;

        await vm.SaveConfigCommand.ExecuteAsync(null);

        _configStore.Verify(c => c.SaveAsync(
            It.Is<AiConfig>(cfg =>
                cfg.BaseUrl == "https://api.deepseek.com" &&
                cfg.Model == "deepseek-flash" &&
                cfg.Detail == AiDetailLevel.Original &&
                cfg.Concurrency == 4 &&
                cfg.MaxRetries == 5 &&
                cfg.AllowUpload &&
                cfg.MaxPreviewDimension == 1536 &&
                cfg.PreviewQuality == 90),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestConnection_Success_SetsStatusTrue()
    {
        var vm = CreateVm();
        _secretStore.Setup(s => s.GetSecretAsync(AiConstants.ApiKeySecretName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("sk-test");
        _client.Setup(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DeepSeekRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeepSeekResponse());

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.True(vm.ConnectionStatus);
        Assert.Contains("成功", vm.StatusText);
        _client.Verify(c => c.ChatAsync(
            It.IsAny<string>(), "sk-test", It.IsAny<DeepSeekRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestConnection_Failure_SetsStatusFalse()
    {
        var vm = CreateVm();
        _secretStore.Setup(s => s.GetSecretAsync(AiConstants.ApiKeySecretName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("sk-bad");
        _client.Setup(c => c.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DeepSeekRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeepSeekException(DeepSeekErrorKind.Authentication, "401 invalid key", 401));

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.False(vm.ConnectionStatus);
        Assert.Contains("失败", vm.StatusText);
    }

    [Fact]
    public void ToggleShowKey_FlipsValue()
    {
        var vm = CreateVm();
        Assert.False(vm.ShowApiKey);

        vm.ToggleShowKeyCommand.Execute(null);
        Assert.True(vm.ShowApiKey);

        vm.ToggleShowKeyCommand.Execute(null);
        Assert.False(vm.ShowApiKey);
    }
}
