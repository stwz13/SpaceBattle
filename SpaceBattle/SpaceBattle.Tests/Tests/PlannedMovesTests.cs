using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class PlannedMovesTests
{
    private sealed class Placements : IRouter<StubServerThread>
    {
        public StubServerThread Target { get; set; } = null!;

        public StubServerThread Route(string key) => Target;
    }

    private sealed class RingLoads(Dictionary<string, (StubServerThread Node, bool InTransit)> games, PlannedMoves<StubServerThread> moves)
        : ILoadMeter<StubServerThread>
    {
        public int Count(StubServerThread node, string exceptKey) =>
            games.Count(pair => pair.Key != exceptKey && ReferenceEquals(Where(pair), node));

        public int Total(string exceptKey) => games.Count(pair => pair.Key != exceptKey);

        private StubServerThread Where(KeyValuePair<string, (StubServerThread Node, bool InTransit)> pair) =>
            moves.Placement(pair.Key, pair.Value.Node, pair.Value.InTransit);
    }

    private static readonly StubServerThread Old = new("old");
    private static readonly StubServerThread Fresh = new("fresh");

    [Fact]
    public void Key_Which_Is_Moving_Counts_On_Its_Destination()
    {
        var moves = new PlannedMoves<StubServerThread>();
        moves.Plan("game", Fresh);

        Assert.Same(Fresh, moves.Placement("game", Old, inTransit: true));
    }

    [Fact]
    public void Key_Which_Is_Not_Moving_Counts_Where_It_Is()
    {
        var moves = new PlannedMoves<StubServerThread>();
        moves.Plan("game", Fresh);

        Assert.Same(Old, moves.Placement("game", Old, inTransit: false));
    }

    [Fact]
    public void Unplanned_Key_Counts_Where_It_Is_Even_When_Moving()
    {
        Assert.Same(Old, new PlannedMoves<StubServerThread>().Placement("game", Old, inTransit: true));
    }

    [Fact]
    public void Later_Plan_Replaces_The_Earlier_One()
    {
        var moves = new PlannedMoves<StubServerThread>();
        moves.Plan("game", Old);
        moves.Plan("game", Fresh);

        Assert.Same(Fresh, moves.Placement("game", Old, inTransit: true));
    }

    [Fact]
    public void Planning_Router_Records_Each_Decision()
    {
        var moves = new PlannedMoves<StubServerThread>();
        var inner = new Placements { Target = Fresh };

        var routed = new PlanningRouter<StubServerThread>(inner, moves).Route("game");

        Assert.Same(Fresh, routed);
        Assert.Same(Fresh, moves.Placement("game", Old, inTransit: true));
    }

    [Fact]
    public void Arguments_Are_Validated()
    {
        var moves = new PlannedMoves<StubServerThread>();

        Assert.Throws<ArgumentNullException>(() => moves.Plan(null!, Old));
        Assert.Throws<ArgumentNullException>(() => moves.Plan("game", null!));
        Assert.Throws<ArgumentNullException>(() => moves.Placement(null!, Old, true));
        Assert.Throws<ArgumentNullException>(() => moves.Placement("game", null!, true));
        Assert.Throws<ArgumentNullException>(() => new PlanningRouter<StubServerThread>(null!, moves));
        Assert.Throws<ArgumentNullException>(() => new PlanningRouter<StubServerThread>(new Placements(), null!));
    }

    private static Dictionary<string, StubServerThread> Rebalance(bool planned)
    {
        var ring = new ConsistentHashRing<StubServerThread>(new FnvHashFunction(), 64);
        ring.Add("old", Old);
        ring.Add("fresh", Fresh);
        var moves = new PlannedMoves<StubServerThread>();
        var games = Enumerable.Range(0, 12).ToDictionary(index => $"game-{index}", _ => (Node: Old, InTransit: false));
        var meter = new RingLoads(games, moves);
        IRouter<StubServerThread> bounded = new BoundedLoadRouter<StubServerThread>(ring, meter, 0, 16);
        var router = planned ? new PlanningRouter<StubServerThread>(bounded, moves) : bounded;
        var targets = new Dictionary<string, StubServerThread>();
        foreach (var id in games.Keys.ToArray())
        {
            var target = router.Route(id);
            targets[id] = target;
            if (!ReferenceEquals(target, games[id].Node))
            {
                games[id] = (games[id].Node, true);
            }
        }
        return targets;
    }

    [Fact]
    public void Without_Planning_One_Rebalancing_Pass_Sends_Every_Game_To_The_New_Node()
    {
        var result = Rebalance(planned: false);

        Assert.Equal(12, result.Values.Count(node => ReferenceEquals(node, Fresh)));
    }

    [Fact]
    public void With_Planning_One_Rebalancing_Pass_Splits_The_Games()
    {
        var result = Rebalance(planned: true);

        Assert.InRange(result.Values.Count(node => ReferenceEquals(node, Fresh)), 5, 7);
        Assert.InRange(result.Values.Count(node => ReferenceEquals(node, Old)), 5, 7);
    }
}
