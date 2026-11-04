namespace SpaceBattle.Lib;

public sealed partial class ManagedGameHost
{
    public void StopForError()
    {
        lock (gate)
        {
            _state = GameHostState.Stopped;
            _game.Paused = true;
        }
    }
}
