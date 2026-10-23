using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class RefuelTests
{
    public RefuelTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(RefuelCommand).Assembly],
            [typeof(IRefuelTarget), typeof(IRefuelSource)], []).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyContextActionComposition().Execute();
        new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
        new RegisterIoCDependencyRefuelContextCommand("Context.Refuel", "Target", "Source").Execute();
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static TestGameItem Ship(int fuel)
    {
        var ship = new TestGameItem();
        ship.SetProperty("Fuel", fuel);
        return ship;
    }

    private static TestGameItem Tanker(int reserve, int amount)
    {
        var tanker = new TestGameItem();
        tanker.SetProperty("FuelReserve", reserve);
        tanker.SetProperty("RefuelAmount", amount);
        return tanker;
    }

    private static ICommandContext Context(IGameItem target, IGameItem source) => new CommandContext(
        new Dictionary<string, IGameItem> { ["Target"] = target, ["Source"] = source });

    private static Lib.ICommand Refuel(ICommandContext context) =>
        Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, "Refuel", context);

    [Fact]
    public void Refuel_Moves_Fuel_From_The_Tanker_To_The_Ship()
    {
        var ship = Ship(1);
        var tanker = Tanker(9, 4);

        Refuel(Context(ship, tanker)).Execute();

        Assert.Equal(5, ship.GetProperty("Fuel"));
        Assert.Equal(5, tanker.GetProperty("FuelReserve"));
        Assert.Equal(4, tanker.GetProperty("RefuelAmount"));
    }

    [Fact]
    public void Refuel_Can_Be_Repeated_And_Gives_The_Rest_At_The_End()
    {
        var ship = Ship(0);
        var tanker = Tanker(9, 4);
        var refuel = Refuel(Context(ship, tanker));

        refuel.Execute();
        refuel.Execute();
        refuel.Execute();

        Assert.Equal(9, ship.GetProperty("Fuel"));
        Assert.Equal(0, tanker.GetProperty("FuelReserve"));

        refuel.Execute();

        Assert.Equal(9, ship.GetProperty("Fuel"));
        Assert.Equal(0, tanker.GetProperty("FuelReserve"));
    }

    [Fact]
    public void Tanker_Gives_Only_What_It_Has()
    {
        var ship = Ship(2);
        var tanker = Tanker(3, 4);

        Refuel(Context(ship, tanker)).Execute();

        Assert.Equal(5, ship.GetProperty("Fuel"));
        Assert.Equal(0, tanker.GetProperty("FuelReserve"));
    }

    [Fact]
    public void Empty_Tanker_Changes_Nothing_And_Does_Not_Fail()
    {
        var ship = Ship(2);
        var tanker = Tanker(0, 4);

        Refuel(Context(ship, tanker)).Execute();

        Assert.Equal(2, ship.GetProperty("Fuel"));
        Assert.Equal(0, tanker.GetProperty("FuelReserve"));
    }

    [Fact]
    public void Creating_Refuel_Returns_A_Macro_Without_Executing_It()
    {
        var ship = Ship(1);
        var tanker = Tanker(9, 4);

        var command = Refuel(Context(ship, tanker));

        Assert.IsType<MacroCommand>(command);
        Assert.Equal(1, ship.GetProperty("Fuel"));
        Assert.Equal(9, tanker.GetProperty("FuelReserve"));
    }

    [Fact]
    public void Refuel_Changes_Only_The_Participants()
    {
        var ship = Ship(1);
        var tanker = Tanker(9, 4);
        var otherShip = Ship(2);
        var otherTanker = Tanker(7, 3);
        var refuel = Refuel(Context(ship, tanker));
        _ = Refuel(Context(otherShip, otherTanker));

        refuel.Execute();

        Assert.Equal(2, otherShip.GetProperty("Fuel"));
        Assert.Equal(7, otherTanker.GetProperty("FuelReserve"));
    }

    [Fact]
    public void Roles_Decide_Who_Gives_And_Who_Receives()
    {
        var first = Tanker(9, 4);
        first.SetProperty("Fuel", 1);
        var second = Tanker(6, 2);
        second.SetProperty("Fuel", 10);

        Refuel(Context(first, second)).Execute();

        Assert.Equal(3, first.GetProperty("Fuel"));
        Assert.Equal(9, first.GetProperty("FuelReserve"));
        Assert.Equal(10, second.GetProperty("Fuel"));
        Assert.Equal(4, second.GetProperty("FuelReserve"));
    }

    [Theory]
    [InlineData("FuelReserve")]
    [InlineData("RefuelAmount")]
    public void Missing_Source_Property_Stops_The_Action_Without_Changing_The_Ship(string property)
    {
        var ship = Ship(1);
        var tanker = Tanker(9, 4);
        tanker.Properties.Remove(property);

        Assert.Throws<KeyNotFoundException>(() => Refuel(Context(ship, tanker)).Execute());

        Assert.Equal(1, ship.GetProperty("Fuel"));
    }

    [Fact]
    public void Missing_Ship_Fuel_Leaves_The_Reserve_Untouched()
    {
        var ship = new TestGameItem();
        var tanker = Tanker(9, 4);

        Assert.Throws<KeyNotFoundException>(() => Refuel(Context(ship, tanker)).Execute());

        Assert.Equal(9, tanker.GetProperty("FuelReserve"));
    }

    [Fact]
    public void Refuel_Amount_Can_Be_Changed_Through_Registration()
    {
        var ship = Ship(1);
        var tanker = Tanker(9, 4);
        var parentScope = Ioc.Resolve<object>("IoC.Scope.Current");
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            Register(GeneratedNames.PropertyDependency(typeof(IRefuelSource),
                typeof(IRefuelSource).GetProperty(nameof(IRefuelSource.RefuelAmount))!), _ => 2);

            Refuel(Context(ship, tanker)).Execute();

            Assert.Equal(3, ship.GetProperty("Fuel"));
            Assert.Equal(7, tanker.GetProperty("FuelReserve"));
            Assert.Equal(4, tanker.GetProperty("RefuelAmount"));
        }
        finally
        {
            SetScope(parentScope);
        }

        Refuel(Context(ship, tanker)).Execute();

        Assert.Equal(7, ship.GetProperty("Fuel"));
        Assert.Equal(3, tanker.GetProperty("FuelReserve"));
    }

    [Fact]
    public void Missing_Role_Fails_When_The_Action_Is_Created()
    {
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = Ship(1) });

        Assert.Throws<KeyNotFoundException>(() => Refuel(context));
    }

    [Fact]
    public void Anything_But_A_Context_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Context.Refuel", Ship(1)));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Context.Refuel"));
    }

    [Theory]
    [InlineData("", "Target", "Source")]
    [InlineData("Context.X", " ", "Source")]
    [InlineData("Context.X", "Target", "")]
    [InlineData("Context.X", "Target", "Target")]
    public void Registration_Rejects_Invalid_Arguments(string key, string target, string source)
    {
        Assert.Throws<ArgumentException>(() => new RegisterIoCDependencyRefuelContextCommand(key, target, source));
    }
}
