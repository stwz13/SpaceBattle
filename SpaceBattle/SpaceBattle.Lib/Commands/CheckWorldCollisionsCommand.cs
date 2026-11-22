namespace SpaceBattle.Lib;

internal sealed class CheckWorldCollisionsCommand(
    IReadOnlyCollection<IGameItem> items,
    Func<IReadOnlyDictionary<IGameItem, MovementSegment>> takeSegments,
    Func<IGameItem, bool> isTarget,
    Func<IGameItem, bool> isSource,
    Func<IGameItem, IGameItem, Func<Vector>, ICommand> createCollision) : ICommand
{
    public void Execute()
    {
        var segments = takeSegments();
        var active = items.Distinct<IGameItem>(ReferenceEqualityComparer.Instance).Where(IsActive).ToArray();
        var targets = active.Where(isTarget).Select(item => (Item: item, Segment: SegmentFor(item, segments))).ToArray();
        var sources = active.Where(isSource).Select(item => (Item: item, Segment: SegmentFor(item, segments))).ToArray();

        foreach (var target in targets)
        {
            foreach (var source in sources)
            {
                if (ReferenceEquals(target.Item, source.Item) || !IsActive(target.Item) || !IsActive(source.Item))
                {
                    continue;
                }

                createCollision(target.Item, source.Item, () => CollisionOffset.Between(target.Segment, source.Segment))
                    .Execute();
            }
        }
    }

    private static MovementSegment SegmentFor(IGameItem item, IReadOnlyDictionary<IGameItem, MovementSegment> segments)
    {
        if (segments.TryGetValue(item, out var segment))
        {
            return segment;
        }

        var position = (Vector)item.GetProperty(CollisionOffset.PositionProperty);
        return new MovementSegment(position, position);
    }

    private static bool IsActive(IGameItem item)
    {
        try
        {
            return item.GetProperty(nameof(IDestructible.Destroyed)) is not true;
        }
        catch (KeyNotFoundException)
        {
            return true;
        }
    }
}
