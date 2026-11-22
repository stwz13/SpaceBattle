namespace SpaceBattle.Lib;

public sealed class WorldMovementSegments
{
    private Dictionary<IGameItem, MovementSegment> _pending = new(ReferenceEqualityComparer.Instance);

    public void Record(IGameItem item, Vector start, Vector end)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        if (!_pending.TryAdd(item, new MovementSegment(start, end)))
        {
            throw new InvalidOperationException("The item already has a movement segment for this tick.");
        }
    }

    internal IReadOnlyDictionary<IGameItem, MovementSegment> Take()
    {
        var segments = _pending;
        _pending = new(ReferenceEqualityComparer.Instance);
        return segments;
    }
}
