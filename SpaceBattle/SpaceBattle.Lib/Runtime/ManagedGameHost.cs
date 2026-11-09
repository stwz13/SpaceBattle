namespace SpaceBattle.Lib;


/// <summary>A stable address for a game. All queue access is confined to its owning thread.</summary>
public sealed partial class ManagedGameHost(
    IManagedGame game, IServerThread thread, IGameRunner runner, object gate,
    Func<IGameRunner, IMessage, ICommand> post, Func<IServerThread, Action, Task> invoke,
    Func<IGame, Action<string>, ICommand> snapshot,
    Func<IGameRunner, IServerThread, Action, Task> enqueue, Action<IGameRunner> wake, IGameHostLifecycle lifecycle) : IManagedGameHost
{
    private IManagedGame _game = game;
    private IServerThread _thread = thread;
    private IGameRunner _runner = runner;
    public IManagedGame Game => _game;
    public IGameRunner Runner => _runner;
    public string Id => _game.Id;
    public IServerThread Thread { get { lock (gate) return _thread; } }
    public GameHostState State { get { lock (gate) return lifecycle.State; } }

    public void Send(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (gate)
        {
            if (message.GameId != Id) throw new MessageFormatException("The message belongs to another game.");
            if (SendWhileUnavailable(message)) return;
            if (lifecycle.State != GameHostState.Running) throw new ServerOverloadedException("The game is suspended.");
            if (!_thread.TryAdd(post(_runner, message)))
                throw new ServerOverloadedException("The game thread is full.");
        }
    }

    private bool SendWhileUnavailable(IMessage message)
    {
        var handled = false;
        HandleUnavailable(message, ref handled);
        return handled;
    }
    partial void HandleUnavailable(IMessage message, ref bool handled);

    public Task Execute(Action action)
    {
        lock (gate)
        {
            var game = _game;
            return invoke(_thread, () =>
            {
                using var entered = game.Scope.Enter();
                action();
            });
        }
    }

    public Task Enqueue(Action action)
    {
        lock (gate)
        {
            if (lifecycle.State != GameHostState.Running) throw new InvalidOperationException("The game is not running.");
            return enqueue(_runner, _thread, action);
        }
    }

    public Task Wake() => invoke(Thread, () =>
    {
        _game.Paused = false;
        // The runner also handles a restored queue which was filled before it was created.
        wake(_runner);
    });

    public async Task<string> PauseAndSnapshot()
    {
        Task paused;
        string? json = null;
        lock (gate)
        {
            if (lifecycle.State != GameHostState.Running) throw new InvalidOperationException("The game is not running.");
            lifecycle.BeginMove();
            paused = invoke(_thread, () =>
            {
                _game.Paused = true;
                using var entered = _game.Scope.Enter();
                snapshot(_game, text => json = text).Execute();
            });
        }
        await paused.ConfigureAwait(false);
        return json!;
    }

    public async Task Resume()
    {
        await Wake().ConfigureAwait(false);
        lock (gate) lifecycle.Resume();
    }

    public void MarkStopped() { lock (gate) lifecycle.Stop(); }
}
