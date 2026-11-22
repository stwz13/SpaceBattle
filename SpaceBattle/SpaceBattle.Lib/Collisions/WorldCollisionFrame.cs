namespace SpaceBattle.Lib;

public sealed class WorldCollisionFrame
{
    private readonly IReadOnlyDictionary<IGameItem, MovementSegment> _segments;

    internal WorldCollisionFrame(IReadOnlyCollection<IGameItem> items,
        IReadOnlyDictionary<IGameItem, MovementSegment> segments)
    {
        Items = items;
        _segments = segments;
    }

    public IReadOnlyCollection<IGameItem> Items { get; }

    public bool TryGetMovement(IGameItem item, out MovementSegment path) => _segments.TryGetValue(item, out path);

    public MovementSegment PathFor(IGameItem item)
    {
        if (TryGetMovement(item, out var path))
            return path;

        var position = (Vector)item.GetProperty(CollisionOffset.PositionProperty);
        return new MovementSegment(position, position);
    }
}
