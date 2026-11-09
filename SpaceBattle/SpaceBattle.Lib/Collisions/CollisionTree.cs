namespace SpaceBattle.Lib;

public class CollisionTree
{
    private readonly IReadOnlyDictionary<int, CollisionTree> _children;
    private readonly bool _terminal;

    public CollisionTree(IEnumerable<IEnumerable<int>> paths)
        : this(paths.Select(path => path.ToArray()).ToList())
    {
    }

    private CollisionTree(IReadOnlyList<int[]> paths)
    {
        _terminal = paths.Any(path => path.Length == 0);
        _children = paths
            .Where(path => path.Length > 0)
            .GroupBy(path => path[0])
            .ToDictionary(
                group => group.Key,
                group => new CollisionTree(group.Select(path => path[1..]).ToList()));
    }

    public bool Contains(IEnumerable<int> path) => Walk(path.ToArray());

    private bool Walk(ReadOnlySpan<int> path) => path.IsEmpty
        ? _terminal
        : _children.TryGetValue(path[0], out var child) && child.Walk(path[1..]);
}
