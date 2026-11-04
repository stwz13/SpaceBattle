namespace SpaceBattle.Lib;

public sealed partial class ManagedGameHost
{
    private List<IMessage>? _buffer;
    private int _bufferLimit;
    private IRemoteServer? _remote;

    partial void HandleUnavailable(IMessage message, ref bool handled)
    {
        if (_state == GameHostState.Moved)
        {
            _remote!.Send(message);
            handled = true;
        }
        else if (_state == GameHostState.Moving && _buffer is not null)
        {
            if (_buffer.Count >= _bufferLimit) throw new ServerOverloadedException("The transfer buffer is full.");
            _buffer.Add(message);
            handled = true;
        }
    }

    public Task<string> BeginTransfer(List<IMessage> buffer, int limit)
    {
        lock (gate)
        {
            if (_state != GameHostState.Running) throw new InvalidOperationException("The game is not running.");
            _buffer = buffer;
            _bufferLimit = limit;
            return PauseAndSnapshot();
        }
    }

    public void CommitTransfer(IGameArrival arrival, IRemoteServer remote)
    {
        lock (gate)
        {
            arrival.Commit(_buffer!.ToArray());
            _remote = remote;
            _buffer = null;
            _state = GameHostState.Moved;
        }
    }

    public Task CancelTransfer()
    {
        lock (gate)
        {
            var pending = _buffer?.Select(message => post(_runner, message)).ToArray() ?? [];
            var resumed = invoke(_thread, () =>
            {
                Array.ForEach(pending, command => command.Execute());
                _game.Paused = false;
                wake(_runner);
            });
            _buffer = null;
            _state = GameHostState.Running;
            return resumed;
        }
    }

    public void Activate(IReadOnlyCollection<IMessage> messages)
    {
        var commands = messages.Select(message => post(_runner, message)).ToArray();
        _ = invoke(_thread, () =>
        {
            Array.ForEach(commands, command => command.Execute());
            _game.Paused = false;
            wake(_runner);
        });
    }
}
