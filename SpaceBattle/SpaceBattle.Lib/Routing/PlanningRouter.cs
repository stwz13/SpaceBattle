namespace SpaceBattle.Lib;

public sealed class PlanningRouter<TNode> : IRouter<TNode>
    where TNode : class
{
    private readonly IRouter<TNode> _inner;
    private readonly PlannedMoves<TNode> _moves;

    public PlanningRouter(IRouter<TNode> inner, PlannedMoves<TNode> moves)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(moves);
        _inner = inner;
        _moves = moves;
    }

    public TNode Route(string key)
    {
        var node = _inner.Route(key);
        _moves.Plan(key, node);
        return node;
    }
}
