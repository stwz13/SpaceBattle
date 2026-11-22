namespace SpaceBattle.Lib;

public sealed class StopCommand : ICommand
{
    private readonly Action _stop;

    internal StopCommand(Action stop)
    {
        ArgumentNullException.ThrowIfNull(stop);
        _stop = stop;
    }

    public void Execute() => _stop();
}
