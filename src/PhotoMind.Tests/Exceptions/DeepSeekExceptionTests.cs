using PhotoMind.Core.Exceptions;

namespace PhotoMind.Tests.Exceptions;

public class DeepSeekExceptionTests
{
    [Theory]
    [InlineData(DeepSeekErrorKind.Server)]
    [InlineData(DeepSeekErrorKind.Network)]
    [InlineData(DeepSeekErrorKind.RateLimit)]
    public void IsRetryable_TransientKinds_ReturnsTrue(DeepSeekErrorKind kind)
    {
        var ex = new DeepSeekException(kind, "transient");
        Assert.True(ex.IsRetryable);
    }

    [Theory]
    [InlineData(DeepSeekErrorKind.Authentication)]
    [InlineData(DeepSeekErrorKind.InsufficientBalance)]
    [InlineData(DeepSeekErrorKind.InvalidResponse)]
    [InlineData(DeepSeekErrorKind.Request)]
    public void IsRetryable_NonTransientKinds_ReturnsFalse(DeepSeekErrorKind kind)
    {
        var ex = new DeepSeekException(kind, "permanent");
        Assert.False(ex.IsRetryable);
    }

    [Fact]
    public void Constructor_PreservesKindAndStatusCode()
    {
        var ex = new DeepSeekException(DeepSeekErrorKind.RateLimit, "限流", statusCode: 429);
        Assert.Equal(DeepSeekErrorKind.RateLimit, ex.Kind);
        Assert.Equal(429, ex.HttpStatusCode);
        Assert.Equal("限流", ex.Message);
    }

    [Fact]
    public void Constructor_IsPhotoMindException()
    {
        var ex = new DeepSeekException(DeepSeekErrorKind.Request, "x");
        Assert.IsAssignableFrom<PhotoMindException>(ex);
    }
}
