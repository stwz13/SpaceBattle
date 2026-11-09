namespace SpaceBattle.Lib;

public class Game : IGame
{
    private readonly IGameQueue _queue;
    private readonly IGameScope _scope;
    private readonly IQuantum _quantum;
    private readonly Func<bool>? _canExecute;

    public Game(string id, IGameQueue queue, IGameScope scope, IQuantum quantum, Func<bool>? canExecute = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(quantum);
        Id = id;
        _queue = queue;
        _scope = scope;
        _quantum = quantum;
        _canExecute = canExecute;
    }

    public string Id { get; }

    public bool HasCommands => _queue.HasCommands;

    public IReadOnlyCollection<ICommand> Pending => _queue.Pending;

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
        .TakeWhile(index => (_canExecute?.Invoke() ?? true) && _queue.HasCommands && (index == 0 || !_quantum.IsOver))
        .Select(_ => _queue.Dequeue());
}
