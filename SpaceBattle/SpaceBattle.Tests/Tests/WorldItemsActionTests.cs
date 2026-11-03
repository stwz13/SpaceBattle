using App;
using App.Scopes;
using SpaceBattle.Lib;
using SpaceBattle.Tests.WorldStepExtensions;

namespace SpaceBattle.Tests;

public class WorldItemsActionTests
{
    public WorldItemsActionTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyWorldStep().Execute();
        new RegisterIoCDependencyBattleContext(new BattleContext("Battle", new GameItemStorage())).Execute();
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static IGameItemStorage Storage() => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage;

    private static Lib.ICommand World() => Ioc.Resolve<Lib.ICommand>(WorldStepDependencies.Command);

    private static IGameItem Item(int value, bool active)
    {
        var item = new GameItemFactory().Create();
        item.SetProperty("Value", value);
        item.SetProperty("Active", active);
        return item;
    }

    [Fact]
    public void Optional_Per_Item_Phase_Uses_Generated_Commands_And_A_Module_Defined_Filter()
    {
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(IncrementWorldItemCommand).Assembly,
            Array.Empty<Dependency>()).Execute();
        Register("Increment.Config", _ => new[] { GeneratedNames.CommandDependency(typeof(IncrementWorldItemCommand)) });
        new RegisterIoCDependencyWorldItemsAction("Phase.Increment", "Increment",
            item => (bool)item.GetProperty("Active")).Execute();
        Register("WorldStep.Config", _ => new[] { "Phase.Increment" });
        var active = Item(1, true);
        var inactive = Item(10, false);
        Storage().Add("active", active);
        Storage().Add("inactive", inactive);
        var world = World();

        world.Execute();
        var added = Item(20, true);
        Storage().Add("added", added);
        world.Execute();

        Assert.Equal(3, active.GetProperty("Value"));
        Assert.Equal(10, inactive.GetProperty("Value"));
        Assert.Equal(21, added.GetProperty("Value"));
    }

    [Fact]
    public void All_Objects_Complete_A_Phase_Before_The_Next_Phase_Begins()
    {
        var first = Item(0, true);
        var second = Item(0, true);
        Storage().Add("first", first);
        Storage().Add("second", second);
        var seen = new List<int[]>();
        Register("Write.Config", _ => new[] { "Item.Write" });
        Register("Read.Config", _ => new[] { "Item.Read" });
        Register("Item.Write", args => new TickTestCommand(() => ((IGameItem)args[0]).SetProperty("Value", 1)));
        Register("Item.Read", _ => new TickTestCommand(() => seen.Add(
            new[] { (int)first.GetProperty("Value"), (int)second.GetProperty("Value") })));
        new RegisterIoCDependencyWorldItemsAction("Phase.Write", "Write").Execute();
        new RegisterIoCDependencyWorldItemsAction("Phase.Read", "Read").Execute();
        Register("WorldStep.Config", _ => new[] { "Phase.Write", "Phase.Read" });

        World().Execute();

        Assert.Equal(2, seen.Count);
        Assert.All(seen, values => Assert.Equal(new[] { 1, 1 }, values));
    }

    [Fact]
    public void Filter_And_Per_Item_Action_Are_Evaluated_When_The_Phase_Executes()
    {
        var item = Item(0, false);
        Storage().Add("item", item);
        Register("Enable.Config", _ => new[] { "Item.Enable" });
        Register("Increment.Config", _ => new[] { "Item.Increment" });
        Register("Item.Enable", args => new TickTestCommand(() => ((IGameItem)args[0]).SetProperty("Active", true)));
        Register("Item.Increment", args => new TickTestCommand(() => ((IGameItem)args[0]).SetProperty("Value", 1)));
        new RegisterIoCDependencyWorldItemsAction("Phase.Enable", "Enable").Execute();
        new RegisterIoCDependencyWorldItemsAction("Phase.Increment", "Increment",
            value => (bool)value.GetProperty("Active")).Execute();
        Register("WorldStep.Config", _ => new[] { "Phase.Enable", "Phase.Increment" });

        World().Execute();

        Assert.Equal(1, item.GetProperty("Value"));
    }

    [Fact]
    public void Empty_World_And_Excluded_Objects_Do_Not_Resolve_Item_Actions()
    {
        new RegisterIoCDependencyWorldItemsAction("Phase.Absent", "NotRegistered", _ => false).Execute();
        Register("WorldStep.Config", _ => new[] { "Phase.Absent" });
        var world = World();
        world.Execute();
        Storage().Add("item", Item(0, true));

        world.Execute();
    }

    [Fact]
    public void Per_Item_Phase_Rejects_Invalid_Configuration_And_Frames()
    {
        Assert.Throws<ArgumentException>(() => new RegisterIoCDependencyWorldItemsAction(" ", "Action"));
        Assert.Throws<ArgumentException>(() => new RegisterIoCDependencyWorldItemsAction("Phase.Action", " "));
        new RegisterIoCDependencyWorldItemsAction("Phase.Action", "Action").Execute();
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Phase.Action"));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Phase.Action", "invalid"));
    }
}
