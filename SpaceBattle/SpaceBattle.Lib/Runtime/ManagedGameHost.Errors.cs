namespace SpaceBattle.Lib;

public sealed partial class ManagedGameHost
{
    public void StopForError()
    {
        lock (gate)
        {
            lifecycle.Stop();
            _game.Paused = true;
        }
    }
}
