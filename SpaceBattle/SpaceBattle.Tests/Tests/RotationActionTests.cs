using App;
using App.Scopes;
using SpaceBattle.Lib;
using SpaceBattle.Tests.RotationExtensions;

namespace SpaceBattle.Tests;

public class RotationActionTests
{
    private readonly IAngleFactory _angles;

    public RotationActionTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyValueFactories().Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(RotateCommand).Assembly, Array.Empty<Dependency>()).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
        _angles = Ioc.Resolve<IAngleFactory>("Factories.Angle");
    }

    private TestGameItem Ship(int direction, int angularVelocity)
    {
        var ship = new TestGameItem();
        ship.Properties["Direction"] = _angles.Create(direction);
        ship.Properties["AngularVelocity"] = _angles.Create(angularVelocity);
        return ship;
    }

    private static Lib.ICommand Rotation(IGameItem ship) =>
        Ioc.Resolve<Lib.ICommand>("Commands.Action", "Rotation", ship);

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(7, 2, 1)]
    [InlineData(0, -1, 7)]
    [InlineData(3, 0, 3)]
    [InlineData(2, 8, 2)]
    [InlineData(1, -3, 6)]
    [InlineData(0, 17, 1)]
    public void Configured_Rotation_Adds_Angular_Velocity_Modulo_A_Full_Turn(
        int direction, int angularVelocity, int expectedDirection)
    {
        var ship = Ship(direction, angularVelocity);
        var velocity = ship.Properties["AngularVelocity"];
        var position = new object();
        ship.Properties["Position"] = position;

        Rotation(ship).Execute();

        Assert.Equal(_angles.Create(expectedDirection), ship.Properties["Direction"]);
        Assert.Same(velocity, ship.Properties["AngularVelocity"]);
        Assert.Same(position, ship.Properties["Position"]);
    }

    [Fact]
    public void Creating_Rotation_Returns_A_Macro_Without_Executing_It()
    {
        var ship = Ship(1, 2);
        var originalDirection = ship.Properties["Direction"];

        var command = Rotation(ship);

        Assert.IsType<MacroCommand>(command);
        Assert.Same(originalDirection, ship.Properties["Direction"]);
        command.Execute();
        Assert.Equal(_angles.Create(3), ship.Properties["Direction"]);
    }

    [Fact]
    public void Repeated_Execution_Reads_The_Current_Direction()
    {
        var ship = Ship(6, 3);
        var command = Rotation(ship);

        command.Execute();
        Assert.Equal(_angles.Create(1), ship.Properties["Direction"]);
        command.Execute();
        Assert.Equal(_angles.Create(4), ship.Properties["Direction"]);
    }

    [Fact]
    public void Rotation_Changes_Only_The_Target_Ship()
    {
        var target = Ship(1, 2);
        var other = Ship(5, 1);
        var otherDirection = other.Properties["Direction"];
        var targetRotation = Rotation(target);
        _ = Rotation(other);

        targetRotation.Execute();

        Assert.Equal(_angles.Create(3), target.Properties["Direction"]);
        Assert.Same(otherDirection, other.Properties["Direction"]);
    }

    [Theory]
    [InlineData("Direction")]
    [InlineData("AngularVelocity")]
    public void Missing_Property_Fails_Without_Changing_The_Ship(string property)
    {
        var ship = Ship(1, 2);
        ship.Properties.Remove(property);
        var original = new Dictionary<string, object>(ship.Properties);
        var command = Rotation(ship);

        Assert.Throws<KeyNotFoundException>(() => command.Execute());

        Assert.Equal(original.Count, ship.Properties.Count);
        Assert.All(original, pair => Assert.Same(pair.Value, ship.Properties[pair.Key]));
    }

    [Theory]
    [InlineData("Direction")]
    [InlineData("AngularVelocity")]
    public void Invalid_Property_Type_Fails_Without_Changing_The_Ship(string property)
    {
        var ship = Ship(1, 2);
        ship.Properties[property] = "not an angle";
        var original = new Dictionary<string, object>(ship.Properties);

        Assert.Throws<InvalidCastException>(() => Rotation(ship).Execute());

        Assert.Equal(original.Count, ship.Properties.Count);
        Assert.All(original, pair => Assert.Same(pair.Value, ship.Properties[pair.Key]));
    }

    [Fact]
    public void Angular_Velocity_Behaviour_Can_Be_Changed_In_A_Child_Scope()
    {
        var ship = Ship(2, 1);
        Rotation(ship).Execute();
        Assert.Equal(_angles.Create(3), ship.Properties["Direction"]);
        var parentScope = Ioc.Resolve<object>("IoC.Scope.Current");
        var property = typeof(IRotating).GetProperty(nameof(IRotating.AngularVelocity))!;
        var key = GeneratedNames.PropertyDependency(typeof(IRotating), property);
        var velocity = _angles.Create(3);

        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        try
        {
            Ioc.Resolve<App.ICommand>("IoC.Register", key, (object[] args) => velocity).Execute();
            Rotation(ship).Execute();
            Assert.Equal(_angles.Create(6), ship.Properties["Direction"]);
            Assert.Equal(_angles.Create(1), ship.Properties["AngularVelocity"]);
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", parentScope).Execute();
        }

        Rotation(ship).Execute();
        Assert.Equal(_angles.Create(7), ship.Properties["Direction"]);
    }

    [Fact]
    public void New_Command_Extends_Rotation_Through_Config_Without_Changing_Existing_Classes()
    {
        new RegisterIoCDependencyGeneratedStrategies(typeof(RecordRotationCommand).Assembly, Array.Empty<Dependency>()).Execute();
        var directory = Directory.CreateTempSubdirectory("SpaceBattle-Rotation-");
        var parentScope = Ioc.Resolve<object>("IoC.Scope.Current");
        try
        {
            File.WriteAllLines(Path.Combine(directory.FullName, "Rotation.config"),
            [
                GeneratedNames.CommandDependency(typeof(RotateCommand)),
                GeneratedNames.CommandDependency(typeof(RecordRotationCommand))
            ]);
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
            new RegisterIoCDependencyActionConfigurations(directory.FullName).Execute();
            var ship = Ship(1, 2);

            Rotation(ship).Execute();

            Assert.Equal(_angles.Create(3), ship.Properties["Direction"]);
            Assert.Same(ship.Properties["Direction"], ship.Properties["DirectionAfterRotation"]);
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", parentScope).Execute();
            directory.Delete(true);
        }

        var standardShip = Ship(1, 2);
        Rotation(standardShip).Execute();
        Assert.Equal(_angles.Create(3), standardShip.Properties["Direction"]);
        Assert.False(standardShip.Properties.ContainsKey("DirectionAfterRotation"));
    }
}
