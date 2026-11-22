namespace SpaceBattle.Lib;

internal sealed class BattleTickDriver
{
    private readonly IManagedGameHost _host;
    private readonly TimeProvider _time;
    private readonly TimeSpan _period;
    private readonly Func<bool> _finished;
    private readonly Action<Exception> _report;
    private readonly LongOperationCommands _ticks;
    private ITimer? _timer;
    private object? _dispatch;

    public BattleTickDriver(IManagedGameHost host, TimeProvider time, TimeSpan period, Func<bool> finished,
        Func<ICommandReceiver, LongOperationCommands> create, Action<Exception> report)
    {
        _host = host;
        _time = time;
        _period = period;
        _finished = finished;
        _report = report;
        _ticks = create(new CommandReceiver(Schedule));
    }

    public void Activate()
    {
        if (!_finished())
            Run(_ticks.Start);
    }

    public void Suspend()
    {
        _ticks.Stop.Execute();
        _dispatch = null;
        _timer?.Dispose();
        _timer = null;
    }

    private void Schedule(ICommand continuation)
    {
        var dispatch = new object();
        _dispatch = dispatch;
        _timer = _time.CreateTimer(_ => { _ = Deliver(dispatch, continuation); }, null, _period, Timeout.InfiniteTimeSpan);
    }

    private async Task Deliver(object dispatch, ICommand continuation)
    {
        try
        {
            await _host.Execute(() =>
            {
                if (!ReferenceEquals(_dispatch, dispatch))
                    return;
                _timer?.Dispose();
                _timer = null;
                _host.Runner.Post(new TransientGameCommand(new CallbackCommand(() =>
                {
                    if (ReferenceEquals(_dispatch, dispatch))
                        Run(continuation);
                })));
            }).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            _report(error);
        }
    }

    private void Run(ICommand command)
    {
        try
        {
            command.Execute();
            if (_finished())
                Suspend();
        }
        catch (Exception error)
        {
            Suspend();
            _report(error);
        }
    }
}
