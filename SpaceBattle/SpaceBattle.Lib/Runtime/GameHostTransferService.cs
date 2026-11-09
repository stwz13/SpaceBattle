namespace SpaceBattle.Lib;

public sealed class GameHostTransferService(IGameHostLifecycle lifecycle, object gate,
    Func<Task<string>> pause, Func<IReadOnlyCollection<IMessage>, Task> resume) : IGameHostTransfer
{
    private List<IMessage>? _buffer;
    private int _limit;
    private IRemoteServer? _remote;
    public IReadOnlyCollection<IMessage> Buffered { get { lock (gate) return _buffer?.ToArray() ?? []; } }
    public bool TrySend(IMessage message)
    {
        lock (gate)
        {
            if (lifecycle.State == GameHostState.Moved) { _remote!.Send(message); return true; }
            if (lifecycle.State != GameHostState.Moving || _buffer is null) return false;
            if (_buffer.Count >= _limit) throw new ServerOverloadedException("The transfer buffer is full.");
            _buffer.Add(message);
            return true;
        }
    }
    public Task<string> Begin(List<IMessage> buffer, int limit)
    {
        lock (gate)
        {
            if (lifecycle.State != GameHostState.Running) throw new InvalidOperationException("The game is not running.");
            _buffer = buffer;
            _limit = limit;
            return pause();
        }
    }
    public void Commit(IGameArrival arrival, IRemoteServer remote)
    {
        lock (gate)
        {
            arrival.Commit(_buffer!.ToArray());
            _remote = remote;
            _buffer = null;
            lifecycle.MarkMoved();
        }
    }
    public Task Cancel()
    {
        lock (gate)
        {
            var resumed = resume(_buffer?.ToArray() ?? []);
            _buffer = null;
            lifecycle.Resume();
            return resumed;
        }
    }
    public void CompleteRelocation()
    {
        lock (gate) { _buffer = null; lifecycle.Resume(); }
    }
}
