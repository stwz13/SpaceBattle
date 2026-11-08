using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

/// <summary>Remembers where keys were sent, so keys which are still moving are counted on their destination.</summary>
public sealed class PlannedMoves<TNode>
    where TNode : class
{
    private readonly ConcurrentDictionary<string, TNode> _destinations = new(StringComparer.Ordinal);

    public void Plan(string key, TNode destination)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(destination);
        _destinations[key] = destination;
    }

    public TNode Placement(string key, TNode current, bool inTransit)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(current);
        return inTransit && _destinations.TryGetValue(key, out var destination) ? destination : current;
    }
}
