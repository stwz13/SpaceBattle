using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib;

public class BlockingCommandQueue : ICommandQueue
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly LinkedList<ICommand> _commands = new();
    private readonly SemaphoreSlim _available = new(0);
    private bool _completed;

    public BlockingCommandQueue(int capacity = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _commands.Count;
            }
        }
    }

    public void Add(ICommand command) => Insert(command, _commands.AddLast, int.MaxValue);

    public bool TryAdd(ICommand command) => Insert(command, _commands.AddLast, _capacity);

    public void AddFirst(ICommand command) => Insert(command, _commands.AddFirst, int.MaxValue);

    public ICommand Take()
    {
        _available.Wait();
        return Dequeue() ?? throw new InvalidOperationException("The queue is completed and empty.");
    }

    public bool TryTake([NotNullWhen(true)] out ICommand? command)
    {
        command = _available.Wait(0) ? Dequeue() : null;
        return command is not null;
    }

    public void CompleteAdding()
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }
            _completed = true;
        }
        _available.Release();
    }

    public IEnumerable<ICommand> Consume() => Enumerable.Range(0, int.MaxValue)
        .Select(_ => TakeOrNull())
        .TakeWhile(command => command is not null)
        .Select(command => command!);

    private ICommand? TakeOrNull()
    {
        _available.Wait();
        return Dequeue();
    }

    private bool Insert(ICommand command, Func<ICommand, LinkedListNode<ICommand>> place, int limit)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            if (_completed)
            {
                throw new InvalidOperationException("The queue has been completed.");
            }
            if (_commands.Count >= limit)
            {
                return false;
            }
            place(command);
        }
        _available.Release();
        return true;
    }

    private ICommand? Dequeue()
    {
        lock (_gate)
        {
            var first = _commands.First;
            if (first is null)
            {
                _available.Release();
                return null;
            }
            _commands.RemoveFirst();
            return first.Value;
        }
    }
}
