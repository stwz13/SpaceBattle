using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class SegmentProjectileCollisionTests
{
    [Fact]
    public void Configured_Kind_Can_Take_The_Ship_Role()
    {
        IBattleShipClassifier classifier = new BattleShipKindClassifier(["Fighter"]);
        var item = new GameItem(new Dictionary<string, object> { ["Kind"] = "Fighter" });
        Assert.True(classifier.IsShip(item));
        Assert.False(classifier.IsShip(new GameItem(new Dictionary<string, object> { ["Kind"] = "Projectile" })));
    }

    [Fact]
    public void Detector_Chooses_The_First_Target_On_An_Entire_Segment()
    {
        IProjectileCollisionDetector detector = new SegmentProjectileCollisionDetector();
        var nearer = Item(2, 2);
        var farther = Item(4, 4);
        var missed = Item(1, 2);
        var origin = new Vector(0, 0);
        Assert.Same(nearer, detector.FirstHit(origin, new Vector(5, 5),
            [farther, missed, nearer, new GameItem(new Dictionary<string, object>())]));
        Assert.Null(detector.FirstHit(origin, origin, [nearer, farther]));
        Assert.Same(nearer, detector.FirstHit(new Vector(2, 2), new Vector(2, 2), [nearer]));
        Assert.Throws<ArgumentException>(() => detector.FirstHit(origin, new Vector(1), []));
        Assert.Equal(new Vector(2, 2), nearer.GetProperty("Position"));
    }

    private static GameItem Item(int x, int y) =>
        new(new Dictionary<string, object> { ["Position"] = new Vector(x, y) });
}
