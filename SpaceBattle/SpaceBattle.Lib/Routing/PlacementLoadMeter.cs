namespace SpaceBattle.Lib;

public sealed class PlacementLoadMeter<TNode> : ILoadMeter<TNode>
    where TNode : class
{
    private readonly Func<IReadOnlyCollection<(string Id, TNode Node)>> _placements;

    public PlacementLoadMeter(Func<IReadOnlyCollection<(string Id, TNode Node)>> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        _placements = placements;
    }

    public int Count(TNode node, string exceptKey)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(exceptKey);
        return _placements().Count(placement => placement.Id != exceptKey && ReferenceEquals(placement.Node, node));
    }

    public int Total(string exceptKey)
    {
        ArgumentNullException.ThrowIfNull(exceptKey);
        return _placements().Count(placement => placement.Id != exceptKey);
    }
}
