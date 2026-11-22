namespace SpaceBattle.Lib;

internal sealed class CheckWorldCollisionsCommand(
    WorldCollisionFrame frame,
    Func<IGameItem, bool> isTarget,
    Func<IGameItem, bool> isSource,
    Func<IGameItem, IGameItem, Func<Vector>, ICommand> createCollision) : ICommand
{
    public void Execute()
    {
        var active = frame.Items.Distinct<IGameItem>(ReferenceEqualityComparer.Instance).Where(IsActive).ToArray();
        var targets = active.Where(isTarget).Select(item => (Item: item, Segment: frame.PathFor(item))).ToArray();
        var sources = active.Where(isSource).Select(item => (Item: item, Segment: frame.PathFor(item))).ToArray();

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
