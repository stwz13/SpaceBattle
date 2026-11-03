namespace SpaceBattle.Lib;

public sealed partial class ManagedGameHost
{
    public void Relocate(IManagedGameHost replacement)
    {
        lock (gate)
        {
            replacement.Activate(_buffer!.ToArray());
            _game = replacement.Game;
            _thread = replacement.Thread;
            _runner = replacement.Runner;
            _buffer = null;
            _state = GameHostState.Running;
        }
    }
}
