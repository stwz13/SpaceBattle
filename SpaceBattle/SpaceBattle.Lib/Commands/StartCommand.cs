namespace SpaceBattle.Lib;

public sealed class StartCommand : ICommand
{
    private readonly Action _start;

    internal StartCommand(Action start)
    {
        ArgumentNullException.ThrowIfNull(start);
        _start = start;
    }

    public void Execute() => _start();
}
