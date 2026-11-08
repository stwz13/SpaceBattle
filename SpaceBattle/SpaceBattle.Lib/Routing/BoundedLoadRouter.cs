namespace SpaceBattle.Lib;

/// <summary>
/// Consistent hashing with bounded loads. A key goes to the owner chosen by the ring unless the owner already
/// holds the permitted share of keys; then the key is hashed again with a salt until a node with room is found.
/// The permitted share is ceil((total + 1) / nodes * (1 + tolerance)).
/// </summary>
public sealed class BoundedLoadRouter<TNode> : IRouter<TNode>
    where TNode : class
{
    private readonly IConsistentHashRing<TNode> _ring;
    private readonly ILoadMeter<TNode> _meter;
    private readonly double _tolerance;
    private readonly int _probes;

    public BoundedLoadRouter(IConsistentHashRing<TNode> ring, ILoadMeter<TNode> meter, double tolerance, int probes)
    {
        ArgumentNullException.ThrowIfNull(ring);
        ArgumentNullException.ThrowIfNull(meter);
        if (double.IsNaN(tolerance) || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "The tolerance must not be negative.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(probes, 1);
        _ring = ring;
        _meter = meter;
        _tolerance = tolerance;
        _probes = probes;
    }

    public TNode Route(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var limit = Limit(key);
        return Candidates(key).FirstOrDefault(node => _meter.Count(node, key) < limit)
            ?? Candidates(key).MinBy(node => _meter.Count(node, key))!;
    }

    private int Limit(string key)
    {
        if (_ring.Count == 0)
        {
            throw new InvalidOperationException("The ring has no nodes.");
        }
        var share = (_meter.Total(key) + 1) / (double)_ring.Count;
        return Math.Max(1, (int)Math.Ceiling(share * (1 + _tolerance)));
    }

    private IEnumerable<TNode> Candidates(string key) => Enumerable.Range(0, _probes)
        .Select(attempt => _ring.Find(attempt == 0 ? key : $"{key}/{attempt}"));
}
