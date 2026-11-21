namespace SpaceBattle.Lib;

public sealed class LongOperationCommands
{
    internal LongOperationCommands(StartCommand start, StopCommand stop)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(stop);
        Start = start;
        Stop = stop;
    }

    public StartCommand Start { get; }
    public StopCommand Stop { get; }
}
