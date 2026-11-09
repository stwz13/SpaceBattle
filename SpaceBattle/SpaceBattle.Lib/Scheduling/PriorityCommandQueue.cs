using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib;

public interface IPrioritizedCommand : ICommand { int Priority { get; } }

public sealed class PrioritizedCommand(ICommand command, int priority) : IPrioritizedCommand
{
    public int Priority { get; } = priority;
    public void Execute() => command.Execute();
}

/// <summary>Linearizes insertions under one lock. Equal priorities are FIFO in insertion order.</summary>
public sealed class PriorityCommandQueue(
    int capacity, object gate, SemaphoreSlim available,
    PriorityQueue<ICommand, (long Priority, long Sequence)> queue) : ICommandQueue
{
    private long _sequence;
    private long _urgent;
    private bool _completed;
    public int Count { get { lock (gate) return queue.Count; } }
    public void Add(ICommand command) => Insert(command, false, false);
    public bool TryAdd(ICommand command) => Insert(command, true, false);
    public void AddFirst(ICommand command) => Insert(command, false, true);
    private bool Insert(ICommand command, bool bounded, bool urgent)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (gate)
        {
            if (_completed) throw new InvalidOperationException("The queue is completed.");
            if (bounded && queue.Count >= capacity) return false;
            var priority = urgent ? long.MinValue : -(long)(command is IPrioritizedCommand item ? item.Priority : 0);
            queue.Enqueue(command, (priority, urgent ? checked(--_urgent) : checked(++_sequence)));
            available.Release();
            return true;
        }
    }
    public ICommand Take() => TakeOrNull() ?? throw new InvalidOperationException("The queue is completed and empty.");
    public bool TryTake([NotNullWhen(true)] out ICommand? command)
    {
        command = available.Wait(0) ? Dequeue() : null;
        return command is not null;
    }
    public void CompleteAdding()
    {
        lock (gate)
        {
            if (_completed) return;
            _completed = true;
            available.Release();
        }
    }
    public IEnumerable<ICommand> Consume() => Enumerable.Range(0, int.MaxValue)
        .Select(_ => TakeOrNull()).TakeWhile(command => command is not null).Select(command => command!);
    private ICommand? TakeOrNull() { available.Wait(); return Dequeue(); }
    private ICommand? Dequeue()
    {
        lock (gate)
        {
            if (queue.TryDequeue(out var command, out _)) return command;
            available.Release();
            return null;
        }
    }
}
