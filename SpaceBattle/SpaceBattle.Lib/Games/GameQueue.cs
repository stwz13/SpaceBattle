namespace SpaceBattle.Lib;

public class GameQueue : IGameQueue
{
    private readonly Queue<ICommand> _commands = new();

    public bool HasCommands => _commands.Count > 0;

    public IReadOnlyCollection<ICommand> Pending => Array.AsReadOnly(_commands.ToArray());

    public void Enqueue(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Enqueue(command);
    }

    public ICommand Dequeue() => _commands.Dequeue();
}
