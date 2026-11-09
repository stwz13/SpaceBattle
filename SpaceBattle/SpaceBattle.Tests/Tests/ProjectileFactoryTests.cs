using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ProjectileFactoryTests
{
    private static ProjectileFactory Factory() => new(new GameItemFactory(), _ => new Vector(0, 0));

    [Fact]
    public void Velocity_Strategy_Receives_The_Initialized_Projectile()
    {
        var expected = new Vector(3, 4);
        var factory = new ProjectileFactory(new GameItemFactory(), item =>
        {
            Assert.Equal(5, item.GetProperty("Speed"));
            Assert.Equal(new Angle(1), item.GetProperty("Direction"));
            return expected;
        });

        var projectile = factory.Create(new Vector(1, 2), new Angle(1), 5, 8);

        Assert.Same(expected, projectile.GetProperty("Velocity"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Position_Must_Have_Two_Dimensions(int dimensions)
    {
        Assert.Throws<ArgumentException>(() => Factory().Create(new Vector(new int[dimensions]), new Angle(0), 1, 1));
    }

    [Fact]
    public void Zero_Speed_And_Damage_Are_Allowed()
    {
        var projectile = Factory().Create(new Vector(0, 0), new Angle(0), 0, 0);

        Assert.Equal(0, projectile.GetProperty("Speed"));
        Assert.Equal(0, projectile.GetProperty("Damage"));
    }

    [Fact]
    public void Null_Arguments_Are_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ProjectileFactory(null!, _ => new Vector(0, 0)));
        Assert.Throws<ArgumentNullException>(() => new ProjectileFactory(new GameItemFactory(), null!));
        Assert.Throws<ArgumentNullException>(() => Factory().Create(null!, new Angle(0), 1, 1));
        Assert.Throws<ArgumentNullException>(() => Factory().Create(new Vector(0, 0), null!, 1, 1));
    }
}
