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
    public void Detector_Chooses_The_Earliest_Contact_Using_Both_Paths()
    {
        IProjectileCollisionDetector detector = new SegmentProjectileCollisionDetector();
        var earlier = Item();
        var later = Item();
        var differentTime = Item();
        var missed = Item();

        Assert.Same(earlier, detector.FirstHit(Path(0, 0, 10, 0), [
            (later, Path(7, 0, 7, 0)),
            (differentTime, Path(3, 5, 3, 0)),
            (missed, Path(2, 1, 2, 1)),
            (earlier, Path(8, 0, 0, 0))
        ]));
        Assert.Null(detector.FirstHit(Path(0, 0, 10, 0), [(differentTime, Path(3, 5, 3, 0))]));
    }

    [Fact]
    public void Detector_Handles_Vertical_Reversed_And_Stationary_Paths()
    {
        IProjectileCollisionDetector detector = new SegmentProjectileCollisionDetector();
        var target = Item();
        Assert.Same(target, detector.FirstHit(Path(0, 10, 0, 0), [(target, Path(0, 2, 0, 2))]));
        Assert.Null(detector.FirstHit(Path(0, 0, 0, 0), [(target, Path(0, 2, 0, 2))]));
        Assert.Same(target, detector.FirstHit(Path(0, 0, 0, 0), [(target, Path(0, 0, 0, 0))]));
        Assert.Same(target, detector.FirstHit(Path(0, 0, 10, 0), [(target, Path(10, 0, 10, 0))]));
    }

    private static MovementSegment Path(int startX, int startY, int endX, int endY) =>
        new(new Vector(startX, startY), new Vector(endX, endY));

    private static GameItem Item() => new(new Dictionary<string, object>());
}
