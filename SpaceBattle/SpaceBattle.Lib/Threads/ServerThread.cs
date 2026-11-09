namespace SpaceBattle.Lib;

public class ServerThread : IServerThread, ICommand
{
    private readonly ICommandProducer _inboxIn;
    private readonly ICommandConsumer _inboxOut;
    private readonly ICommandQueue _loop;
    private readonly IScheduler _scheduler;
    private readonly IExceptionHandler _handler;
    private readonly IExceptionFactory _exceptions;
    private Action _behaviour;
    private bool _running = true;
    private int _threadId;
    private int _started;

    public ServerThread(ICommandQueue inbox, ICommandQueue loop, IScheduler scheduler,
        IExceptionHandler handler, IExceptionFactory exceptions)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(exceptions);
        _inboxIn = inbox;
        _inboxOut = inbox;
        _loop = loop;
        _scheduler = scheduler;
        _handler = handler;
        _exceptions = exceptions;
        _behaviour = Process;
    }

    public int ThreadId => Volatile.Read(ref _threadId);

    public Task Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            throw _exceptions.InvalidOperation("The server thread has already been started.");
        }
        return Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
    }

    public void Run()
    {
        Volatile.Write(ref _threadId, Environment.CurrentManagedThreadId);
        _loop.Add(this);
        foreach (var tick in _loop.Consume())
        {
            tick.Execute();
        }
    }

    public void Execute()
    {
        _behaviour();
        if (_running)
        {
            _loop.Add(this);
        }
        else
        {
            _loop.CompleteAdding();
        }
    }

    public void Add(ICommand command) => _inboxIn.Add(command);

    public bool TryAdd(ICommand command) => _inboxIn.TryAdd(command);

    public void Interrupt(ICommand command) => _inboxIn.AddFirst(command);

    public void Schedule(ICommand command) => _scheduler.Add(command);

    public void UpdateBehaviour(Action behaviour)
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        _behaviour = behaviour;
    }

    public void HardStop()
    {
        _inboxIn.CompleteAdding();
        _running = false;
    }

    public void SoftStop()
    {
        _inboxIn.CompleteAdding();
        UpdateBehaviour(Drain);
    }

    private void Process()
    {
        if (_scheduler.HasCommand())
        {
            RunNew();
            RunScheduled();
        }
        else
        {
            Handle(_inboxOut.Take());
        }
    }

    private void Drain()
    {
        if (_inboxOut.Count == 0 && !_scheduler.HasCommand())
        {
            _running = false;
            return;
        }
        RunNew();
        RunScheduled();
    }

    private void RunNew()
    {
        if (_inboxOut.TryTake(out var command))
        {
            Handle(command);
        }
    }

    private void RunScheduled()
    {
        if (_running && _scheduler.HasCommand())
        {
            Handle(_scheduler.Select());
        }
    }

    private void Handle(ICommand command)
    {
        try
        {
            command.Execute();
        }
        catch (Exception exception)
        {
            _handler.Handle(command, exception);
        }
    }
}
