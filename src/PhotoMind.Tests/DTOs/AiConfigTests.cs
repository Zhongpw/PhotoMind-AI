using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;

namespace PhotoMind.Tests.DTOs;

public class AiConfigTests
{
    [Fact]
    public void Sanitized_ConcurrencyTooLow_ClampedToMin()
    {
        var config = new AiConfig { Concurrency = 0 };
        config.Sanitized();
        Assert.Equal(AiConstants.MinConcurrency, config.Concurrency);
    }

    [Fact]
    public void Sanitized_ConcurrencyTooHigh_ClampedToMax()
    {
        var config = new AiConfig { Concurrency = 99 };
        config.Sanitized();
        Assert.Equal(AiConstants.MaxConcurrency, config.Concurrency);
    }

    [Fact]
    public void Sanitized_MaxRetriesNegative_ClampedToZero()
    {
        var config = new AiConfig { MaxRetries = -3 };
        config.Sanitized();
        Assert.Equal(0, config.MaxRetries);
    }

    [Fact]
    public void Sanitized_MaxRetriesTooHigh_ClampedToTen()
    {
        var config = new AiConfig { MaxRetries = 500 };
        config.Sanitized();
        Assert.Equal(10, config.MaxRetries);
    }

    [Fact]
    public void Sanitized_MaxPreviewDimensionTooSmall_ClampedTo128()
    {
        var config = new AiConfig { MaxPreviewDimension = 32 };
        config.Sanitized();
        Assert.Equal(128, config.MaxPreviewDimension);
    }

    [Fact]
    public void Sanitized_MaxPreviewDimensionTooLarge_ClampedTo4096()
    {
        var config = new AiConfig { MaxPreviewDimension = 10000 };
        config.Sanitized();
        Assert.Equal(4096, config.MaxPreviewDimension);
    }

    [Fact]
    public void Sanitized_PreviewQualityOutOfRange_Clamped()
    {
        var low = new AiConfig { PreviewQuality = 1 }.Sanitized();
        var high = new AiConfig { PreviewQuality = 500 }.Sanitized();
        Assert.Equal(10, low.PreviewQuality);
        Assert.Equal(100, high.PreviewQuality);
    }

    [Fact]
    public void Sanitized_EmptyBaseUrlAndModel_FilledWithDefaults()
    {
        var config = new AiConfig { BaseUrl = "", Model = "   " };
        config.Sanitized();
        Assert.Equal(AiConstants.DefaultBaseUrl, config.BaseUrl);
        Assert.Equal(AiConstants.DefaultModel, config.Model);
    }

    [Fact]
    public void Sanitized_ValidValues_Unchanged()
    {
        var config = new AiConfig { Concurrency = 3, MaxRetries = 3, MaxPreviewDimension = 1024, PreviewQuality = 85 };
        config.Sanitized();
        Assert.Equal(3, config.Concurrency);
        Assert.Equal(3, config.MaxRetries);
        Assert.Equal(1024, config.MaxPreviewDimension);
        Assert.Equal(85, config.PreviewQuality);
    }
}
