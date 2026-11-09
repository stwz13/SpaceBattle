namespace SpaceBattle.Lib;

public sealed class GameHostLifecycle(object gate) : IGameHostLifecycle
{
    private GameHostState _state;
    public GameHostState State { get { lock (gate) return _state; } }
    public void BeginMove()
    {
        lock (gate)
        {
            if (_state != GameHostState.Running) throw new InvalidOperationException("The game is not running.");
            _state = GameHostState.Moving;
        }
    }
    public void Resume() { lock (gate) _state = GameHostState.Running; }
    public void MarkMoved() { lock (gate) _state = GameHostState.Moved; }
    public void Stop() { lock (gate) _state = GameHostState.Stopped; }
}
