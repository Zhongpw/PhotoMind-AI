namespace PhotoMind.Core.Threading;

/// <summary>
/// Cooperative pause mechanism for background work (analogous to CancellationToken, but for pause/resume).
/// The worker awaits <see cref="PauseToken.WaitWhilePausedAsync"/> at safe points between units of work.
/// </summary>
public sealed class PauseTokenSource
{
    private readonly object _sync = new();
    private TaskCompletionSource<bool>? _resume;

    public bool IsPaused
    {
        get { lock (_sync) return _resume is not null; }
    }

    public PauseToken Token => new(this);

    public void Pause()
    {
        lock (_sync)
        {
            _resume ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        TaskCompletionSource<bool>? tcs;
        lock (_sync)
        {
            tcs = _resume;
            _resume = null;
        }
        tcs?.TrySetResult(true);
    }

    internal Task WaitWhilePausedAsync()
    {
        TaskCompletionSource<bool>? tcs;
        lock (_sync)
        {
            tcs = _resume;
        }
        return tcs?.Task ?? Task.CompletedTask;
    }
}

public readonly struct PauseToken
{
    private readonly PauseTokenSource? _source;

    internal PauseToken(PauseTokenSource source) => _source = source;

    public bool IsPaused => _source?.IsPaused ?? false;

    public Task WaitWhilePausedAsync() => _source?.WaitWhilePausedAsync() ?? Task.CompletedTask;
}
