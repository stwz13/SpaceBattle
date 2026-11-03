namespace SpaceBattle.Lib;

public enum GameHostState { Running, Moving, Moved, Stopped }

/// <summary>A stable address for a game. All queue access is confined to its owning thread.</summary>
public sealed partial class ManagedGameHost(
    IManagedGame game, IServerThread thread, IGameRunner runner, object gate,
    Func<IGameRunner, IMessage, ICommand> post, Func<IServerThread, Action, Task> invoke,
    Func<IGame, Action<string>, ICommand> snapshot,
    Func<IGameRunner, IServerThread, Action, Task> enqueue, Action<IGameRunner> wake) : IManagedGameHost
{
    private IManagedGame _game = game;
    private IServerThread _thread = thread;
    private IGameRunner _runner = runner;
    private GameHostState _state;
    public IManagedGame Game => _game;
    public IGameRunner Runner => _runner;
    public string Id => _game.Id;
    public IServerThread Thread { get { lock (gate) return _thread; } }
    public GameHostState State { get { lock (gate) return _state; } }

    public void Send(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (gate)
        {
            if (message.GameId != Id) throw new MessageFormatException("The message belongs to another game.");
            if (SendWhileUnavailable(message)) return;
            if (_state != GameHostState.Running) throw new ServerOverloadedException("The game is suspended.");
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
            if (_state != GameHostState.Running) throw new InvalidOperationException("The game is not running.");
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
            if (_state != GameHostState.Running) throw new InvalidOperationException("The game is not running.");
            _state = GameHostState.Moving;
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
        lock (gate) _state = GameHostState.Running;
    }

    public void MarkStopped() { lock (gate) _state = GameHostState.Stopped; }
}
