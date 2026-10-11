using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MovementTests
{
    public MovementTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(MoveCommand).Assembly, []).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
    }

    private TestGameItem Ship(int fuel, int consumption)
    {
        var ship = new TestGameItem();
        ship.Properties["Position"] = new Vector(12, 5);
        ship.Properties["Velocity"] = new Vector(-7, 3);
        ship.Properties["Fuel"] = fuel;
        ship.Properties["FuelConsumption"] = consumption;
        return ship;
    }

    private static Lib.ICommand Movement(IGameItem ship) =>
        Ioc.Resolve<Lib.ICommand>("Commands.Action", "Movement", ship);

    [Fact]
    public void Movement_Moves_Object_And_Burns_Fuel()
    {
        var ship = Ship(10, 3);

        Movement(ship).Execute();

        Assert.Equal(new Vector(5, 8), ship.Properties["Position"]);
        Assert.Equal(7, ship.Properties["Fuel"]);
    }

    [Fact]
    public void Movement_Can_Be_Repeated_While_Fuel_Lasts()
    {
        var ship = Ship(7, 3);
        var movement = Movement(ship);

        movement.Execute();
        movement.Execute();

        Assert.Equal(new Vector(-2, 11), ship.Properties["Position"]);
        Assert.Equal(1, ship.Properties["Fuel"]);
        Assert.Throws<InvalidOperationException>(movement.Execute);
        Assert.Equal(new Vector(-2, 11), ship.Properties["Position"]);
        Assert.Equal(1, ship.Properties["Fuel"]);
    }

    [Fact]
    public void Movement_Without_Fuel_Does_Not_Move_And_Does_Not_Burn()
    {
        var ship = Ship(2, 3);
        var position = ship.Properties["Position"];

        var error = Assert.Throws<InvalidOperationException>(Movement(ship).Execute);

        Assert.Equal("Not enough fuel to move.", error.Message);
        Assert.Same(position, ship.Properties["Position"]);
        Assert.Equal(2, ship.Properties["Fuel"]);
    }

    [Fact]
    public void Creating_Movement_Returns_A_Macro_Without_Executing_It()
    {
        var ship = Ship(10, 3);
        var position = ship.Properties["Position"];

        var command = Movement(ship);

        Assert.IsType<MacroCommand>(command);
        Assert.Same(position, ship.Properties["Position"]);
        Assert.Equal(10, ship.Properties["Fuel"]);
    }

    [Theory]
    [InlineData("Position")]
    [InlineData("Velocity")]
    [InlineData("Fuel")]
    [InlineData("FuelConsumption")]
    public void Missing_Property_Stops_Action_Without_Changing_The_Ship(string property)
    {
        var ship = Ship(10, 3);
        ship.Properties.Remove(property);
        var original = new Dictionary<string, object>(ship.Properties);

        Assert.Throws<KeyNotFoundException>(() => Movement(ship).Execute());

        Assert.Equal(original.Count, ship.Properties.Count);
        Assert.All(original, pair => Assert.Same(pair.Value, ship.Properties[pair.Key]));
    }

    [Fact]
    public void Infinite_Fuel_Is_Configured_Without_Changing_Commands()
    {
        var directory = Directory.CreateTempSubdirectory("SpaceBattle-Movement-");
        var parentScope = Ioc.Resolve<object>("IoC.Scope.Current");
        try
        {
            File.WriteAllLines(Path.Combine(directory.FullName, "Movement.config"),
                [GeneratedNames.CommandDependency(typeof(MoveCommand))]);
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
            new RegisterIoCDependencyActionConfigurations(directory.FullName).Execute();
            var ship = Ship(0, 3);

            Movement(ship).Execute();

            Assert.Equal(new Vector(5, 8), ship.Properties["Position"]);
            Assert.Equal(0, ship.Properties["Fuel"]);
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", parentScope).Execute();
            directory.Delete(true);
        }

        var standardShip = Ship(0, 3);
        Assert.Throws<InvalidOperationException>(() => Movement(standardShip).Execute());
        Assert.Equal(new Vector(12, 5), standardShip.Properties["Position"]);
    }
}
