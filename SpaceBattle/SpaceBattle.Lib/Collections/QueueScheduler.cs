namespace SpaceBattle.Lib;

public class QueueScheduler : IScheduler
{
    private readonly Queue<ICommand> _commands = new();

    public bool HasCommand() => _commands.Count > 0;

    public ICommand Select() => _commands.Dequeue();

    public void Add(ICommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        _commands.Enqueue(cmd);
    }
}
