namespace SpaceBattle.Lib;

public class ConsistentHashRing<TNode> : IConsistentHashRing<TNode>
    where TNode : class
{
    private readonly IHashFunction _hash;
    private readonly int _replicas;
    private readonly object _gate = new();
    private Snapshot _snapshot = Snapshot.Empty;

    public ConsistentHashRing(IHashFunction hash, int replicas)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentOutOfRangeException.ThrowIfLessThan(replicas, 1);
        _hash = hash;
        _replicas = replicas;
    }

    public int Count => Volatile.Read(ref _snapshot).Nodes.Count;

    public void Add(string id, TNode node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(node);
        lock (_gate)
        {
            var nodes = _snapshot.Nodes;
            if (nodes.ContainsKey(id))
            {
                throw new ArgumentException($"The node '{id}' is already on the ring.", nameof(id));
            }
            Volatile.Write(ref _snapshot, Build(nodes.Append(new KeyValuePair<string, TNode>(id, node))));
        }
    }

    public bool Remove(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            var nodes = _snapshot.Nodes;
            if (!nodes.ContainsKey(id))
            {
                return false;
            }
            Volatile.Write(ref _snapshot, Build(nodes.Where(pair => pair.Key != id)));
            return true;
        }
    }

    public TNode Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot.Points.Length == 0)
        {
            throw new InvalidOperationException("The ring has no nodes.");
        }
        var found = Array.BinarySearch(snapshot.Points, _hash.Hash(key));
        var index = found < 0 ? ~found : found;
        return snapshot.Nodes[snapshot.Owners[index % snapshot.Points.Length]];
    }

    private Snapshot Build(IEnumerable<KeyValuePair<string, TNode>> nodes)
    {
        var nodeMap = nodes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var positions = nodeMap.Keys
            .SelectMany(id => Enumerable.Range(0, _replicas).Select(replica => (Id: id, Point: _hash.Hash($"{id}#{replica}"))))
            .GroupBy(position => position.Point)
            .Select(group => group.OrderBy(position => position.Id, StringComparer.Ordinal).First())
            .OrderBy(position => position.Point)
            .ToArray();
        return new Snapshot(nodeMap, positions.Select(position => position.Point).ToArray(),
            positions.Select(position => position.Id).ToArray());
    }

    private sealed class Snapshot(IReadOnlyDictionary<string, TNode> nodes, ulong[] points, string[] owners)
    {
        public static Snapshot Empty { get; } = new(new Dictionary<string, TNode>(), [], []);

        public IReadOnlyDictionary<string, TNode> Nodes { get; } = nodes;

        public ulong[] Points { get; } = points;

        public string[] Owners { get; } = owners;
    }
}
