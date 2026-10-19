using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MovementComputedVelocityTests
{
    public MovementComputedVelocityTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyComputedVelocity().Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(MoveCommand).Assembly,
        [MovementDependencies.ComputedVelocity]).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
    }

    private static TestGameItem Ship(int speed, int direction)
    {
        var ship = new TestGameItem();
        ship.Properties["Position"] = new Vector(12, 5);
        ship.Properties["Speed"] = speed;
        ship.Properties["Direction"] = new Angle(direction);
        ship.Properties["Fuel"] = 10;
        ship.Properties["FuelConsumption"] = 3;
        return ship;
    }

    private static Lib.ICommand Movement(IGameItem ship) =>
        Ioc.Resolve<Lib.ICommand>("Commands.Action", "Movement", ship);

    [Fact]
    public void Dependency_Binds_The_Velocity_Of_IMoving_To_The_Computed_Velocity_Strategy()
    {
        var dependency = MovementDependencies.ComputedVelocity;

        Assert.Equal(typeof(IMoving), dependency.Interface);
        Assert.Equal(nameof(IMoving.Velocity), dependency.Property);
        Assert.Equal(ComputedVelocityDependencies.Velocity, dependency.Strategy);
    }

    [Theory]
    [InlineData(10, 0, 22, 5)]
    [InlineData(10, 2, 12, 15)]
    [InlineData(10, 4, 2, 5)]
    [InlineData(10, 6, 12, -5)]
    public void Movement_Moves_The_Ship_By_The_Velocity_Computed_From_Speed_And_Direction(
        int speed, int direction, int x, int y)
    {
        var ship = Ship(speed, direction);

        Movement(ship).Execute();

        Assert.Equal(new Vector(x, y), ship.Properties["Position"]);
        Assert.Equal(7, ship.Properties["Fuel"]);
        Assert.False(ship.Properties.ContainsKey("Velocity"));
    }

    [Fact]
    public void Movement_Without_Speed_Fails_And_Leaves_The_Ship_Unchanged()
    {
        var ship = Ship(10, 0);
        ship.Properties.Remove("Speed");

        Assert.Throws<KeyNotFoundException>(() => Movement(ship).Execute());

        Assert.Equal(new Vector(12, 5), ship.Properties["Position"]);
        Assert.Equal(10, ship.Properties["Fuel"]);
    }
}
