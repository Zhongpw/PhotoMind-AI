using Moq;
using PhotoMind.App.ViewModels;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.App.Tests.ViewModels;

public class ApiStatusDetectorTests
{
    private readonly Mock<ISecretStore> _store = new();

    [Fact]
    public async Task Detect_NoSecret_ReturnsNotConfigured()
    {
        _store.Setup(s => s.HasSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await ApiStatusDetector.DetectAsync(_store.Object);

        Assert.Equal(ApiStatusKind.NotConfigured, result);
    }

    [Fact]
    public async Task Detect_ReadableSecret_ReturnsConfigured()
    {
        _store.Setup(s => s.HasSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _store.Setup(s => s.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sk-test-123");

        var result = await ApiStatusDetector.DetectAsync(_store.Object);

        Assert.Equal(ApiStatusKind.Configured, result);
    }

    [Fact]
    public async Task Detect_UndecryptableSecret_ReturnsError()
    {
        // Entry exists but decryption fails (GetSecretAsync returns null on CryptographicException).
        _store.Setup(s => s.HasSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _store.Setup(s => s.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await ApiStatusDetector.DetectAsync(_store.Object);

        Assert.Equal(ApiStatusKind.Error, result);
    }

    [Fact]
    public async Task Detect_EmptySecret_ReturnsError()
    {
        _store.Setup(s => s.HasSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _store.Setup(s => s.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var result = await ApiStatusDetector.DetectAsync(_store.Object);

        Assert.Equal(ApiStatusKind.Error, result);
    }

    [Theory]
    [InlineData(ApiStatusKind.NotConfigured, "未配置")]
    [InlineData(ApiStatusKind.Configured, "已配置")]
    [InlineData(ApiStatusKind.Connected, "已连接")]
    [InlineData(ApiStatusKind.Failed, "连接失败")]
    [InlineData(ApiStatusKind.Error, "密钥异常")]
    public void ToText_MapsEveryKind(ApiStatusKind kind, string expected)
    {
        Assert.Equal(expected, ApiStatusDetector.ToText(kind));
    }
}
