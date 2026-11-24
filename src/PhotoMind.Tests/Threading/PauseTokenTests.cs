using PhotoMind.Core.Threading;

namespace PhotoMind.Tests.Threading;

public class PauseTokenTests
{
    [Fact]
    public void Initially_NotPaused()
    {
        var source = new PauseTokenSource();
        Assert.False(source.IsPaused);
        Assert.False(source.Token.IsPaused);
    }

    [Fact]
    public async Task WaitWhilePausedAsync_NotPaused_CompletesImmediately()
    {
        var source = new PauseTokenSource();
        await source.Token.WaitWhilePausedAsync(); // should not block
    }

    [Fact]
    public async Task Pause_SetsIsPaused_AndWaitBlocksUntilResume()
    {
        var source = new PauseTokenSource();
        source.Pause();

        Assert.True(source.IsPaused);
        Assert.True(source.Token.IsPaused);

        var waitTask = source.Token.WaitWhilePausedAsync();
        Assert.False(waitTask.IsCompleted);

        source.Resume();
        await waitTask;
        Assert.False(source.IsPaused);
    }

    [Fact]
    public async Task MultiplePauseResume_CyclesWork()
    {
        var source = new PauseTokenSource();

        for (var i = 0; i < 3; i++)
        {
            source.Pause();
            Assert.True(source.IsPaused);
            source.Resume();
            Assert.False(source.IsPaused);
            await source.Token.WaitWhilePausedAsync();
        }
    }
}
