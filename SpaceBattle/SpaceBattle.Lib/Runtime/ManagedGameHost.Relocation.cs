namespace SpaceBattle.Lib;

public sealed partial class ManagedGameHost
{
    public void Relocate(IManagedGameHost replacement)
    {
        lock (gate)
        {
            replacement.Activate(TransferControl.Buffered);
            _game = replacement.Game;
            _thread = replacement.Thread;
            _runner = replacement.Runner;
            Activation = replacement is ManagedGameHost managed ? managed.Activation : GameActivation.None;
            TransferControl.CompleteRelocation();
        }
    }
}
