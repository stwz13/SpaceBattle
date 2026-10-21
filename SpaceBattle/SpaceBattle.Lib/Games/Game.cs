namespace SpaceBattle.Lib;

public class Game : IGame
{
    private readonly IGameQueue _queue;
    private readonly IGameScope _scope;
    private readonly IQuantum _quantum;

    public Game(string id, IGameQueue queue, IGameScope scope, IQuantum quantum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(quantum);
        Id = id;
        _queue = queue;
        _scope = scope;
        _quantum = quantum;
    }

    public string Id { get; }

    public bool HasCommands => _queue.HasCommands;

    public void Post(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _queue.Enqueue(command);
    }

    public void Execute()
    {
        using var scope = _scope.Enter();
        _quantum.Start();
        foreach (var command in Slice())
        {
            command.Execute();
        }
    }

    private IEnumerable<ICommand> Slice() => Enumerable.Range(0, int.MaxValue)
        .TakeWhile(index => _queue.HasCommands && (index == 0 || !_quantum.IsOver))
        .Select(_ => _queue.Dequeue());
}
