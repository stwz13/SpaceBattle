using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class PlacementLoadMeterTests
{
    private static readonly StubServerThread First = new("first");
    private static readonly StubServerThread Second = new("second");

    private static PlacementLoadMeter<StubServerThread> Meter() => new(() =>
        [("a", First), ("b", First), ("c", Second)]);

    [Fact]
    public void Counts_Games_Placed_On_A_Node()
    {
        Assert.Equal(2, Meter().Count(First, "none"));
        Assert.Equal(1, Meter().Count(Second, "none"));
    }

    [Fact]
    public void Excluded_Game_Is_Not_Counted()
    {
        Assert.Equal(1, Meter().Count(First, "a"));
        Assert.Equal(2, Meter().Total("a"));
    }

    [Fact]
    public void Total_Counts_Every_Placement()
    {
        Assert.Equal(3, Meter().Total("none"));
    }

    [Fact]
    public void Arguments_Are_Validated()
    {
        Assert.Throws<ArgumentNullException>(() => new PlacementLoadMeter<StubServerThread>(null!));
        Assert.Throws<ArgumentNullException>(() => Meter().Count(null!, "a"));
        Assert.Throws<ArgumentNullException>(() => Meter().Count(First, null!));
        Assert.Throws<ArgumentNullException>(() => Meter().Total(null!));
    }
}
