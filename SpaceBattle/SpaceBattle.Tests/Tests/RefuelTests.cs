using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class RefuelTests
{
    public RefuelTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(RefuelCommand).Assembly,
        [
            new Dependency(typeof(IRefuelCheckable), nameof(IRefuelCheckable.ExceptionFactory), "Factories.Exception")
        ]).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
    }

    private static TestGameItem Ship(int fuel, int reserve, int amount)
    {
        var ship = new TestGameItem();
        ship.Properties["Fuel"] = fuel;
        ship.Properties["FuelReserve"] = reserve;
        ship.Properties["RefuelAmount"] = amount;
        return ship;
    }

    private static Lib.ICommand Refuel(IGameItem ship) =>
        Ioc.Resolve<Lib.ICommand>("Commands.Action", "Refuel", ship);

    [Fact]
    public void Refuel_Fills_Fuel_From_Reserve()
    {
        var ship = Ship(1, 9, 4);

        Refuel(ship).Execute();

        Assert.Equal(5, ship.Properties["Fuel"]);
        Assert.Equal(5, ship.Properties["FuelReserve"]);
    }

    [Fact]
    public void Refuel_Can_Be_Repeated_Until_Reserve_Runs_Out()
    {
        var ship = Ship(0, 9, 4);
        var refuel = Refuel(ship);

        refuel.Execute();
        refuel.Execute();

        Assert.Equal(8, ship.Properties["Fuel"]);
        Assert.Equal(1, ship.Properties["FuelReserve"]);
        Assert.Throws<InvalidOperationException>(refuel.Execute);
        Assert.Equal(8, ship.Properties["Fuel"]);
        Assert.Equal(1, ship.Properties["FuelReserve"]);
    }

    [Fact]
    public void Refuel_Without_Reserve_Does_Not_Change_Fuel()
    {
        var ship = Ship(2, 3, 4);

        Assert.Throws<InvalidOperationException>(Refuel(ship).Execute);

        Assert.Equal(2, ship.Properties["Fuel"]);
        Assert.Equal(3, ship.Properties["FuelReserve"]);
    }

    [Fact]
    public void Creating_Refuel_Returns_A_Macro_Without_Executing_It()
    {
        var ship = Ship(1, 9, 4);

        var command = Refuel(ship);

        Assert.IsType<MacroCommand>(command);
        Assert.Equal(1, ship.Properties["Fuel"]);
        Assert.Equal(9, ship.Properties["FuelReserve"]);
    }

    [Theory]
    [InlineData("FuelReserve")]
    [InlineData("RefuelAmount")]
    public void Missing_Source_Property_Stops_Action_Without_Changing_The_Ship(string property)
    {
        var ship = Ship(1, 9, 4);
        ship.Properties.Remove(property);
        var original = new Dictionary<string, object>(ship.Properties);

        Assert.Throws<KeyNotFoundException>(() => Refuel(ship).Execute());

        Assert.Equal(original.Count, ship.Properties.Count);
        Assert.All(original, pair => Assert.Same(pair.Value, ship.Properties[pair.Key]));
    }

    [Fact]
    public void Refuel_Changes_Only_The_Target_Ship()
    {
        var target = Ship(1, 9, 4);
        var other = Ship(2, 7, 3);
        var targetRefuel = Refuel(target);
        _ = Refuel(other);

        targetRefuel.Execute();

        Assert.Equal(5, target.Properties["Fuel"]);
        Assert.Equal(5, target.Properties["FuelReserve"]);
        Assert.Equal(2, other.Properties["Fuel"]);
        Assert.Equal(7, other.Properties["FuelReserve"]);
    }

    [Fact]
    public void Refuel_Amount_Can_Be_Changed_Through_Registration()
    {
        var ship = Ship(1, 9, 4);
        var parentScope = Ioc.Resolve<object>("IoC.Scope.Current");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        try
        {
            foreach (var view in new[] { typeof(IRefuelCheckable), typeof(IRefuelable) })
            {
                var property = view.GetProperty("RefuelAmount")!;
                Ioc.Resolve<App.ICommand>("IoC.Register", GeneratedNames.PropertyDependency(view, property),
                    (object[] args) => (object)2).Execute();
            }

            Refuel(ship).Execute();

            Assert.Equal(3, ship.Properties["Fuel"]);
            Assert.Equal(7, ship.Properties["FuelReserve"]);
            Assert.Equal(4, ship.Properties["RefuelAmount"]);
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", parentScope).Execute();
        }

        Refuel(ship).Execute();
        Assert.Equal(7, ship.Properties["Fuel"]);
        Assert.Equal(3, ship.Properties["FuelReserve"]);
    }

    [Fact]
    public void Failure_Uses_The_Registered_Exception_Factory()
    {
        var factory = new Moq.Mock<IExceptionFactory>();
        var expected = new InvalidOperationException("custom failure");
        factory.Setup(f => f.InvalidOperation("Not enough fuel in reserve to refuel.")).Returns(expected);
        var parentScope = Ioc.Resolve<object>("IoC.Scope.Current");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        try
        {
            Ioc.Resolve<App.ICommand>("IoC.Register", "Factories.Exception",
                (object[] args) => (object)factory.Object).Execute();
            var ship = Ship(2, 3, 4);

            var actual = Assert.Throws<InvalidOperationException>(() => Refuel(ship).Execute());

            Assert.Same(expected, actual);
            Assert.Equal(2, ship.Properties["Fuel"]);
            Assert.Equal(3, ship.Properties["FuelReserve"]);
            factory.Verify(f => f.InvalidOperation("Not enough fuel in reserve to refuel."), Moq.Times.Once);
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", parentScope).Execute();
        }
    }
}
