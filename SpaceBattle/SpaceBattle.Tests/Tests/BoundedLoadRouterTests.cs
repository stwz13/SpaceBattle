using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class BoundedLoadRouterTests
{
    private sealed class Placements : ILoadMeter<StubServerThread>
    {
        public Dictionary<string, StubServerThread> Games { get; } = new();

        public int Count(StubServerThread node, string exceptKey) =>
            Games.Count(pair => pair.Key != exceptKey && ReferenceEquals(pair.Value, node));

        public int Total(string exceptKey) => Games.Count(pair => pair.Key != exceptKey);
    }

    private static ConsistentHashRing<StubServerThread> Ring(int nodes, int replicas = 64)
    {
        var ring = new ConsistentHashRing<StubServerThread>(new FnvHashFunction(), replicas);
        foreach (var index in Enumerable.Range(0, nodes))
        {
            ring.Add($"Thread-{index}", new StubServerThread($"Thread-{index}"));
        }
        return ring;
    }

    private static IEnumerable<string> Keys(int count) => Enumerable.Range(0, count).Select(index => $"game-{index}");

    [Fact]
    public void Key_Goes_To_The_Ring_Owner_While_The_Owner_Has_Room()
    {
        var ring = Ring(3);
        var router = new BoundedLoadRouter<StubServerThread>(ring, new Placements(), 0.25, 16);

        Assert.All(Keys(50), key => Assert.Same(ring.Find(key), router.Route(key)));
    }

    [Fact]
    public void Overloaded_Owner_Is_Skipped()
    {
        var ring = Ring(3);
        var meter = new Placements();
        var owner = ring.Find("game-0");
        Keys(1).Concat(Enumerable.Range(0, 9).Select(index => $"busy-{index}"))
            .Where(key => key != "game-0").ToList().ForEach(key => meter.Games[key] = owner);
        var router = new BoundedLoadRouter<StubServerThread>(ring, meter, 0, 16);

        Assert.NotSame(owner, router.Route("game-0"));
    }

    [Fact]
    public void Routing_Is_Deterministic_For_The_Same_Loads()
    {
        var ring = Ring(4);
        var meter = new Placements();
        var router = new BoundedLoadRouter<StubServerThread>(ring, meter, 0.1, 16);
        Keys(100).ToList().ForEach(key => meter.Games[key] = router.Route(key));

        Assert.All(Keys(100), key => Assert.Same(router.Route(key), router.Route(key)));
    }

    [Fact]
    public void Placed_Key_Is_Not_Counted_Against_Its_Own_Node()
    {
        var ring = Ring(2);
        var meter = new Placements();
        var router = new BoundedLoadRouter<StubServerThread>(ring, meter, 0, 16);
        meter.Games["game-0"] = router.Route("game-0");

        Assert.Same(meter.Games["game-0"], router.Route("game-0"));
    }

    [Fact]
    public void Loads_Stay_Within_The_Permitted_Share_Where_The_Plain_Ring_Is_Skewed()
    {
        const int games = 1000;
        const int nodes = 4;
        var ring = Ring(nodes);
        var meter = new Placements();
        var router = new BoundedLoadRouter<StubServerThread>(ring, meter, 0.05, 16);

        Keys(games).ToList().ForEach(key => meter.Games[key] = router.Route(key));

        var bounded = meter.Games.Values.GroupBy(node => node.Name).Max(group => group.Count());
        var plain = Keys(games).GroupBy(key => ring.Find(key).Name).Max(group => group.Count());
        Assert.True(bounded <= (int)Math.Ceiling((double)games / nodes * 1.05), $"bounded max was {bounded}");
        Assert.True(plain > bounded, $"plain max {plain} should exceed bounded max {bounded}");
        Assert.Equal(games, meter.Games.Count);
    }

    private sealed class SaltedRing(params StubServerThread[] nodes) : IConsistentHashRing<StubServerThread>
    {
        public int Count => nodes.Length;

        public void Add(string id, StubServerThread node) => throw new NotSupportedException();

        public bool Remove(string id) => throw new NotSupportedException();

        public StubServerThread Find(string key) =>
            nodes[key.Contains('/') ? int.Parse(key[(key.IndexOf('/') + 1)..]) % nodes.Length : 0];
    }

    private sealed class FixedLoads(Dictionary<StubServerThread, int> loads, int total = 0) : ILoadMeter<StubServerThread>
    {
        public int Count(StubServerThread node, string exceptKey) => loads[node];

        public int Total(string exceptKey) => total;
    }

    [Fact]
    public void When_Every_Candidate_Is_Overloaded_The_Least_Loaded_One_Is_Chosen()
    {
        var heavy = new StubServerThread("heavy");
        var middle = new StubServerThread("middle");
        var light = new StubServerThread("light");
        var loads = new FixedLoads(new() { [heavy] = 5, [middle] = 3, [light] = 2 });
        var router = new BoundedLoadRouter<StubServerThread>(new SaltedRing(heavy, middle, light), loads, 0, 3);

        Assert.Same(light, router.Route("game"));
    }

    [Fact]
    public void Probing_Moves_On_To_The_Next_Salted_Candidate_Until_One_Has_Room()
    {
        var heavy = new StubServerThread("heavy");
        var free = new StubServerThread("free");
        var loads = new FixedLoads(new() { [heavy] = 5, [free] = 0 });
        var router = new BoundedLoadRouter<StubServerThread>(new SaltedRing(heavy, free), loads, 0, 4);

        Assert.Same(free, router.Route("game"));
    }

    [Fact]
    public void A_Node_Holding_Exactly_The_Permitted_Share_Is_Full()
    {
        var first = new StubServerThread("first");
        var second = new StubServerThread("second");
        var ring = new SaltedRing(first, second);

        var full = new BoundedLoadRouter<StubServerThread>(ring, new FixedLoads(new() { [first] = 1, [second] = 0 }), 0, 4);
        var room = new BoundedLoadRouter<StubServerThread>(ring, new FixedLoads(new() { [first] = 0, [second] = 0 }), 0, 4);

        Assert.Same(second, full.Route("game"));
        Assert.Same(first, room.Route("game"));
    }

    [Fact]
    public void Tolerance_Widens_The_Permitted_Share()
    {
        var first = new StubServerThread("first");
        var second = new StubServerThread("second");
        var ring = new SaltedRing(first, second);
        var loads = new FixedLoads(new() { [first] = 2, [second] = 1 }, total: 3);

        Assert.Same(second, new BoundedLoadRouter<StubServerThread>(ring, loads, 0, 4).Route("game"));
        Assert.Same(first, new BoundedLoadRouter<StubServerThread>(ring, loads, 0.5, 4).Route("game"));
    }

    [Fact]
    public void Empty_Ring_Cannot_Route()
    {
        var router = new BoundedLoadRouter<StubServerThread>(Ring(0), new Placements(), 0.25, 4);

        Assert.Throws<InvalidOperationException>(() => router.Route("game"));
    }

    [Fact]
    public void Arguments_Are_Validated()
    {
        var ring = Ring(1);
        var meter = new Placements();

        Assert.Throws<ArgumentNullException>(() => new BoundedLoadRouter<StubServerThread>(null!, meter, 0, 1));
        Assert.Throws<ArgumentNullException>(() => new BoundedLoadRouter<StubServerThread>(ring, null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedLoadRouter<StubServerThread>(ring, meter, -0.1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedLoadRouter<StubServerThread>(ring, meter, double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedLoadRouter<StubServerThread>(ring, meter, 0, 0));
        Assert.Throws<ArgumentNullException>(() => new BoundedLoadRouter<StubServerThread>(ring, meter, 0, 1).Route(null!));
    }
}
