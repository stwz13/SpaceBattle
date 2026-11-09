using System.Diagnostics.CodeAnalysis;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class SpyCommandQueue(ICommandQueue inner) : ICommandQueue
{
    private int _takes;
    private int _tryTakes;

    public int Takes => Volatile.Read(ref _takes);

    public int TryTakes => Volatile.Read(ref _tryTakes);

    public int Count => inner.Count;

    public void Add(ICommand command) => inner.Add(command);

    public bool TryAdd(ICommand command) => inner.TryAdd(command);

    public void AddFirst(ICommand command) => inner.AddFirst(command);

    public ICommand Take()
    {
        Interlocked.Increment(ref _takes);
        return inner.Take();
    }

    public bool TryTake([NotNullWhen(true)] out ICommand? command)
    {
        Interlocked.Increment(ref _tryTakes);
        return inner.TryTake(out command);
    }

    public void CompleteAdding() => inner.CompleteAdding();

    public IEnumerable<ICommand> Consume() => inner.Consume();
}
