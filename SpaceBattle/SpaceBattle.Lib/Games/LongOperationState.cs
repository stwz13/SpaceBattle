namespace SpaceBattle.Lib;

internal sealed class LongOperationState
{
    private readonly ICommand _empty;
    private CommandInjectableCommand? _active;

    public LongOperationState(ICommand empty)
    {
        ArgumentNullException.ThrowIfNull(empty);
        _empty = empty;
    }

    public void Start(Func<(CommandInjectableCommand Continuation, ICommand Send)> createRun)
    {
        if (_active is not null)
            return;

        var (continuation, send) = createRun();
        _active = continuation;
        try
        {
            send.Execute();
        }
        catch
        {
            StopIfCurrent(continuation);
            throw;
        }
    }

    public bool IsCurrent(CommandInjectableCommand continuation) => ReferenceEquals(_active, continuation);

    public void StopIfCurrent(CommandInjectableCommand continuation)
    {
        if (IsCurrent(continuation))
            Stop();
    }

    public void Stop()
    {
        _active?.Inject(_empty);
        _active = null;
    }
}
