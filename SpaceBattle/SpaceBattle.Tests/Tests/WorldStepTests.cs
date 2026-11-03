using App;
using App.Scopes;
using SpaceBattle.Lib;
using SpaceBattle.Tests.WorldStepExtensions;

namespace SpaceBattle.Tests;

public class WorldStepTests
{
    public WorldStepTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyWorldStep().Execute();
        new RegisterIoCDependencyBattleContext(new BattleContext("Battle", new GameItemStorage())).Execute();
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static void Config(params string[] phases) => Register("WorldStep.Config", _ => phases);

    private static Lib.ICommand World() => Ioc.Resolve<Lib.ICommand>(WorldStepDependencies.Command);

    private static IBattleContext Battle() => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context);

    private static IReadOnlyCollection<IGameItem> Items(IGameItem frame) =>
        (IReadOnlyCollection<IGameItem>)frame.GetProperty(WorldStepDependencies.ItemsProperty);

    [Fact]
    public void Resolving_World_Command_Does_Not_Read_The_World_Or_Run_Phases()
    {
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        var frames = 0;
        Register(WorldStepDependencies.Frame, _ => { frames++; throw new InvalidOperationException(); });

        var command = World();

        Assert.IsType<WorldStepCommand>(command);
        Assert.Equal(0, frames);
    }

    [Fact]
    public void Config_File_Defines_Phases_And_Each_Execution_Creates_A_New_Shared_Frame()
    {
        var directory = Directory.CreateTempSubdirectory("SpaceBattle-WorldStep-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "WorldStep.config"), "Phase.Prepare\nPhase.Apply");
            new RegisterIoCDependencyActionConfigurations(directory.FullName).Execute();
            var frames = new List<IGameItem>();
            var events = new List<string>();
            Register("Phase.Prepare", args =>
            {
                var frame = (IGameItem)args[0];
                frames.Add(frame);
                return new TickTestCommand(() =>
                {
                    events.Add("prepare");
                    frame.SetProperty("Module.State", 42);
                });
            });
            Register("Phase.Apply", args =>
            {
                var frame = (IGameItem)args[0];
                Assert.Same(frames[^1], frame);
                return new TickTestCommand(() =>
                {
                    Assert.Equal(42, frame.GetProperty("Module.State"));
                    events.Add("apply");
                });
            });
            var world = World();

            world.Execute();
            world.Execute();

            Assert.Equal(new[] { "prepare", "apply", "prepare", "apply" }, events);
            Assert.Equal(2, frames.Count);
            Assert.NotSame(frames[0], frames[1]);
            Assert.Same(Battle(), frames[0].GetProperty(WorldStepDependencies.BattleProperty));
            Assert.Throws<KeyNotFoundException>(() => Battle().Storage.Get("Module.State"));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Config_Can_Replace_And_Remove_Mechanics_Without_Recreating_World_Command()
    {
        var events = new List<string>();
        Register("Phase.Grow", _ => new TickTestCommand(() => events.Add("grow")));
        Register("Phase.Weather", _ => new TickTestCommand(() => events.Add("weather")));
        var world = World();
        string[] phases = ["Phase.Grow", "Phase.Weather"];
        Register("WorldStep.Config", _ => phases);
        world.Execute();
        phases = ["Phase.Weather"];
        world.Execute();
        phases = [];
        world.Execute();

        Assert.Equal(new[] { "grow", "weather", "weather" }, events);
    }

    [Fact]
    public void Generated_Adapters_Expose_The_Frame_Without_Handwritten_Adapters()
    {
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategies(typeof(SummarizeWorldFrameCommand).Assembly,
            Array.Empty<Dependency>()).Execute();
        var ship = new GameItemFactory().Create();
        Battle().Storage.Add("ship", ship);
        IGameItem? captured = null;
        Register("Phase.Capture", args => new TickTestCommand(() => captured = (IGameItem)args[0]));
        Config(GeneratedNames.CommandDependency(typeof(SummarizeWorldFrameCommand)), "Phase.Capture");

        World().Execute();

        Assert.Equal("Battle:1", captured!.GetProperty("Summary"));
        Assert.Same(ship, Assert.Single(Items(captured)));
    }

    [Fact]
    public void Frame_Freezes_Membership_For_All_Phases_And_Refreshes_It_Next_Tick()
    {
        var oldItem = new GameItemFactory().Create();
        var newItem = new GameItemFactory().Create();
        var storage = Battle().Storage;
        storage.Add("old", oldItem);
        var observed = new List<IGameItem[]>();
        Register("Phase.Replace", _ => new TickTestCommand(() =>
        {
            if (storage.Remove("old"))
                storage.Add("new", newItem);
        }));
        Register("Phase.Read", args => new TickTestCommand(() => observed.Add(Items((IGameItem)args[0]).ToArray())));
        Config("Phase.Replace", "Phase.Read");
        var world = World();

        world.Execute();
        world.Execute();

        Assert.Same(oldItem, Assert.Single(observed[0]));
        Assert.Same(newItem, Assert.Single(observed[1]));
    }

    [Fact]
    public void Frame_Does_Not_Clone_Object_Properties()
    {
        var item = new GameItemFactory().Create();
        item.SetProperty("Value", 0);
        Battle().Storage.Add("item", item);
        var values = new List<int>();
        Register("Phase.Write", _ => new TickTestCommand(() => item.SetProperty("Value", 7)));
        Register("Phase.Read", args => new TickTestCommand(() =>
            values.Add((int)Assert.Single(Items((IGameItem)args[0])).GetProperty("Value"))));
        Config("Phase.Write", "Phase.Read");

        World().Execute();

        Assert.Equal(new[] { 7 }, values);
    }

    [Fact]
    public void Same_World_Command_Uses_Current_Battle_Config_And_Child_Phase_Overrides()
    {
        var events = new List<string>();
        Register("Phase.Record", args =>
        {
            var battle = (IBattleContext)((IGameItem)args[0]).GetProperty(WorldStepDependencies.BattleProperty);
            return new TickTestCommand(() => events.Add(battle.Id));
        });
        Config("Phase.Record");
        var world = World();
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        var first = Ioc.Resolve<object>("IoC.Scope.Create");
        var second = Ioc.Resolve<object>("IoC.Scope.Create");
        try
        {
            SetScope(first);
            new RegisterIoCDependencyBattleContext(new BattleContext("First", new GameItemStorage())).Execute();
            world.Execute();
            SetScope(second);
            new RegisterIoCDependencyBattleContext(new BattleContext("Second", new GameItemStorage())).Execute();
            Register("Phase.Extra", _ => new TickTestCommand(() => events.Add("extra")));
            Config("Phase.Record", "Phase.Extra");
            world.Execute();
            SetScope(first);
            world.Execute();
        }
        finally
        {
            SetScope(parent);
        }
        world.Execute();

        Assert.Equal(new[] { "First", "Second", "extra", "First", "Battle" }, events);
    }

    [Fact]
    public void Custom_Frame_Strategy_Can_Add_Module_Data_Without_Changing_Core()
    {
        var original = Ioc.Resolve<IGameItem>(WorldStepDependencies.Frame);
        original.SetProperty("Weather.Seed", 123);
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        Register(WorldStepDependencies.Frame, _ => original);
        var seen = 0;
        Register("Phase.Weather", args => new TickTestCommand(() =>
            seen = (int)((IGameItem)args[0]).GetProperty("Weather.Seed")));
        Config("Phase.Weather");

        World().Execute();

        Assert.Equal(123, seen);
    }

    [Fact]
    public void Missing_Config_Or_Phase_Is_An_Error_Not_A_Silent_Empty_Update()
    {
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        var action = "MissingWorld." + Guid.NewGuid().ToString("N");
        new RegisterIoCDependencyWorldStep(action).Execute();
        var world = World();
        Assert.ThrowsAny<Exception>(world.Execute);
        var executed = false;
        Register("Phase.First", _ => new TickTestCommand(() => executed = true));
        Register(action + ".Config", _ => new[] { "Phase.First", "Phase.Missing." + action });

        Assert.ThrowsAny<Exception>(world.Execute);

        Assert.False(executed);
    }

    [Fact]
    public void Phase_Error_Propagates_And_Later_Phases_Are_Not_Executed()
    {
        var failure = new InvalidOperationException("module failed");
        var executed = false;
        Register("Phase.Fail", _ => new TickTestCommand(() => throw failure));
        Register("Phase.After", _ => new TickTestCommand(() => executed = true));
        Config("Phase.Fail", "Phase.After");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(World().Execute));
        Assert.False(executed);
    }

    [Fact]
    public void World_Action_Name_Is_Configurable()
    {
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyWorldStep("Garden").Execute();
        var executed = false;
        Register("Garden.Config", _ => new[] { "Phase.Grow" });
        Register("Phase.Grow", _ => new TickTestCommand(() => executed = true));

        World().Execute();

        Assert.True(executed);
    }

    [Fact]
    public void World_Dependency_Arguments_Are_Validated()
    {
        Assert.Throws<ArgumentException>(() => new RegisterIoCDependencyWorldStep(" "));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(WorldStepDependencies.Command, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IWorldStepFactory>(WorldStepDependencies.Factory, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameItem>(WorldStepDependencies.Frame, 1));
        Assert.Throws<ArgumentNullException>(() => new WorldStepFactory(null!, _ => new TickTestCommand(() => { })));
        Assert.Throws<ArgumentNullException>(() => new WorldStepFactory(() => new GameItemFactory().Create(), null!));
    }
}
