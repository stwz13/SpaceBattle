namespace SpaceBattle.Lib;

public interface IRemoteServer
{
    void Send(IMessage message);
    Task<IGameArrival> Receive(string snapshot);
}

/// <summary>Receive prepares an inactive game. Commit atomically publishes it; Abort discards it.</summary>
public interface IGameArrival
{
    bool IsCommitted { get; }
    void Commit(IReadOnlyCollection<IMessage> buffered);
    void Abort();
}

public sealed class GameArrival(object gate, Action<IReadOnlyCollection<IMessage>> commit, Action abort) : IGameArrival
{
    private int _state;
    public bool IsCommitted { get { lock (gate) return _state == 1; } }
    public void Commit(IReadOnlyCollection<IMessage> buffered)
    {
        lock (gate)
        {
            if (_state == 1) return;
            if (_state == 2) throw new InvalidOperationException("The transfer was aborted.");
            commit(buffered);
            _state = 1;
        }
    }
    public void Abort()
    {
        lock (gate)
        {
            if (_state != 0) return;
            abort();
            _state = 2;
        }
    }
}
