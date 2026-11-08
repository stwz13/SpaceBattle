using System.Text.Json;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class WorldPhaseConfigurationTests : IDisposable
{
    private readonly string _path = Path.GetTempFileName();

    public WorldPhaseConfigurationTests()
    {
        new InitCommand().Execute();
        ChildScope();
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyWorldStep().Execute();
        new RegisterIoCDependencyWorldPhaseConfigurationLoader().Execute();
        new RegisterIoCDependencyBattleContext(new BattleContext("Battle", new GameItemStorage())).Execute();
    }

    public void Dispose() => File.Delete(_path);

    private static void ChildScope() => Ioc.Resolve<App.ICommand>(
        "IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private void Write(string json) => File.WriteAllText(_path, json);

    private void Configure(string action = "WorldStep") =>
        new RegisterIoCDependencyConfiguredWorldPhases(_path, action).Execute();

    private static Lib.ICommand World() => Ioc.Resolve<Lib.ICommand>(WorldStepDependencies.Command);

    [Fact]
    public void Loads_Ordered_Descriptions_And_Registers_Loader()
    {
        Write("""
            {"phases":[
              {"name":"Phase.Prepare","execution":"world","action":"Prepare"},
              {"name":"Phase.Move","execution":"items","action":"Move","selector":"Selectors.Moving"}
            ]}
            """);
        var loader = Ioc.Resolve<IWorldPhaseConfigurationLoader>(WorldPhaseConfigurationDependencies.Loader);

        var phases = loader.Load(_path);

        Assert.Equal(new WorldPhaseDescription("Phase.Prepare", WorldPhaseExecution.World, "Prepare", null), phases[0]);
        Assert.Equal(new WorldPhaseDescription("Phase.Move", WorldPhaseExecution.Items, "Move", "Selectors.Moving"), phases[1]);
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IWorldPhaseConfigurationLoader>(
            WorldPhaseConfigurationDependencies.Loader, "unexpected"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"phases\":null}")]
    [InlineData("{\"phases\":[],\"unknown\":true}")]
    [InlineData("{\"phases\":[],\"phases\":[]}")]
    [InlineData("{\"phases\":[null]}")]
    [InlineData("{\"phases\":[{}]}")]
    [InlineData("""{"phases":[{"name":" ","execution":"world","action":"A"}]}""")]
    [InlineData("""{"phases":[{"name":"P","execution":"world","action":42}]}""")]
    [InlineData("""{"phases":[{"name":"P","execution":"other","action":"A"}]}""")]
    [InlineData("""{"phases":[{"name":"P","execution":"items","action":"A"}]}""")]
    [InlineData("""{"phases":[{"name":"P","execution":"items","action":"A","selector":null}]}""")]
    [InlineData("""{"phases":[{"name":"P","execution":"world","action":"A","selector":"S"}]}""")]
    [InlineData("""{"phases":[{"name":"P","execution":"world","action":"A","extra":true}]}""")]
    [InlineData("""{"phases":[{"name":"P","name":"Q","execution":"world","action":"A"}]}""")]
    [InlineData("""{"phases":[{"name":"P","execution":"world","action":"A"},{"name":"P","execution":"world","action":"B"}]}""")]
    public void Rejects_Invalid_Descriptions(string json)
    {
        Write(json);

        Assert.Throws<FormatException>(() => new JsonWorldPhaseConfigurationLoader().Load(_path));
    }

    [Fact]
    public void Reports_Malformed_Json_And_Missing_Files()
    {
        Write("{");
        Assert.ThrowsAny<JsonException>(() => new JsonWorldPhaseConfigurationLoader().Load(_path));
        File.Delete(_path);
        Assert.Throws<FileNotFoundException>(() => new JsonWorldPhaseConfigurationLoader().Load(_path));
    }

    [Fact]
    public void Mixed_Phases_Run_In_Order_With_Shared_Frame_And_Fresh_Items_Per_Step()
    {
        Write("""
            {"phases":[
              {"name":"Phase.Prepare","execution":"world","action":"Prepare"},
              {"name":"Phase.Move","execution":"items","action":"Move","selector":"Selectors.Moving"},
              {"name":"Phase.Finish","execution":"world","action":"Finish"}
            ]}
            """);
        var storage = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage;
        var first = new GameItemFactory().Create();
        var second = new GameItemFactory().Create();
        storage.Add("first", first);
        storage.Add("second", second);
        var selected = first;
        var moved = new List<IGameItem>();
        var events = new List<string>();
        var frames = new List<IGameItem>();
        Register("Prepare.Config", _ => new[] { "Commands.Prepare" });
        Register("Move.Config", _ => new[] { "Commands.Move" });
        Register("Finish.Config", _ => new[] { "Commands.Finish" });
        Register("Selectors.Moving", _ => (Func<IGameItem, bool>)(item => ReferenceEquals(item, selected)));
        Register("Commands.Prepare", args => new TickTestCommand(() =>
        {
            frames.Add((IGameItem)args[0]);
            ((IGameItem)args[0]).SetProperty("Module.Ready", true);
            events.Add("prepare");
        }));
        Register("Commands.Move", args => new TickTestCommand(() =>
        {
            moved.Add((IGameItem)args[0]);
            events.Add("move");
        }));
        Register("Commands.Finish", args => new TickTestCommand(() =>
        {
            Assert.Same(frames[^1], args[0]);
            Assert.Equal(true, ((IGameItem)args[0]).GetProperty("Module.Ready"));
            events.Add("finish");
        }));
        Configure();
        var world = World();

        world.Execute();
        var third = new GameItemFactory().Create();
        storage.Add("third", third);
        selected = third;
        world.Execute();

        Assert.Equal(new[] { "prepare", "move", "finish", "prepare", "move", "finish" }, events);
        Assert.Equal(new[] { first, third }, moved);
        Assert.NotSame(frames[0], frames[1]);
        Assert.Equal(3, ((IReadOnlyCollection<IGameItem>)frames[1].GetProperty("Items")).Count);
    }

    [Fact]
    public void Selector_Is_Resolved_In_Current_Game_Scope()
    {
        Write("""{"phases":[{"name":"Phase.Items","execution":"items","action":"ItemAction","selector":"Selectors.Active"}]}""");
        var item = new GameItemFactory().Create();
        Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Add("item", item);
        var count = 0;
        Register("ItemAction.Config", _ => new[] { "Commands.Count" });
        Register("Commands.Count", _ => new TickTestCommand(() => count++));
        Register("Selectors.Active", _ => (Func<IGameItem, bool>)(_ => false));
        Configure();
        var world = World();
        world.Execute();
        ChildScope();
        Register("Selectors.Active", _ => (Func<IGameItem, bool>)(_ => true));

        world.Execute();

        Assert.Equal(1, count);
    }

    [Fact]
    public void Empty_Configuration_And_Custom_World_Action_Are_Supported()
    {
        Write("{\"phases\":[]}");
        Configure("Garden");
        ChildScope();
        new RegisterIoCDependencyWorldStep("Garden").Execute();

        World().Execute();

        Assert.Empty(Ioc.Resolve<string[]>("Garden.Config"));
    }

    [Fact]
    public void Validates_Entire_File_Before_Registering_Any_Phases()
    {
        Write("""{"phases":[{"name":"Phase.Valid","execution":"world","action":"A"},{}]}""");
        Assert.Throws<FormatException>(() => Configure());
        var key = WorldStepDependencies.Phase("WorldStep", "Phase.Valid");
        Register(key, _ => new TickTestCommand(() => { }));
        Assert.IsType<TickTestCommand>(Ioc.Resolve<Lib.ICommand>(key));
    }

    [Theory]
    [InlineData("WorldStep.Config", "A")]
    [InlineData("Commands.Action", "A")]
    [InlineData("Commands.Macro", "A")]
    [InlineData("Phase.Recursive", "WorldStep")]
    public void Rejects_Composition_Conflicts(string name, string action)
    {
        Write($$"""{"phases":[{"name":"{{name}}","execution":"world","action":"{{action}}"}]}""");
        Assert.Throws<FormatException>(() => Configure());
    }

    [Fact]
    public void World_Phase_And_Config_Reject_Invalid_Arguments_And_Config_Is_Defensively_Copied()
    {
        Write("""{"phases":[{"name":"Phase.World","execution":"world","action":"A"}]}""");
        Configure();
        var key = WorldStepDependencies.Phase("WorldStep", "Phase.World");
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(key));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(key, 42));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<string[]>("WorldStep.Config", 42));
        Ioc.Resolve<string[]>("WorldStep.Config")[0] = "Changed";
        Assert.Equal(new[] { key }, Ioc.Resolve<string[]>("WorldStep.Config"));
    }

    [Fact]
    public void Configured_Phases_Cannot_Replace_Tick_Commands_And_Can_Reuse_Names_Across_Actions()
    {
        using var scheduler = new DelayedCommandScheduler(new TickTestClock(), _ => { }, _ => { });
        new RegisterIoCDependencyGameTicks(scheduler).Execute();
        var ticks = new GameTickFactory(scheduler).Create(TimeSpan.FromMilliseconds(50),
            new TickTestCommand(() => { }), _ => { });
        var name = GameTickDependencies.Stop;
        Write($$"""{"phases":[{"name":"{{name}}","execution":"world","action":"Update"}]}""");
        Configure();
        Configure("OtherWorld");
        ChildScope();
        Configure();

        Assert.IsType<StopGameTicksCommand>(Ioc.Resolve<Lib.ICommand>(name, ticks));
        Assert.Equal(new[] { WorldStepDependencies.Phase("WorldStep", name) },
            Ioc.Resolve<string[]>("WorldStep.Config"));
        Assert.Equal(new[] { WorldStepDependencies.Phase("OtherWorld", name) },
            Ioc.Resolve<string[]>("OtherWorld.Config"));
    }

    [Fact]
    public void Modules_Can_Register_An_Execution_Mode_And_Use_It_In_Json()
    {
        ChildScope();
        var executions = 0;
        var mode = new WorldPhaseMode(new WorldPhaseExecution("custom"), false, (phase, frame) =>
        {
            Assert.Equal("Update", phase.Action);
            Assert.NotNull(frame.GetProperty(WorldStepDependencies.BattleProperty));
            return new TickTestCommand(() => executions++);
        });
        new RegisterIoCDependencyWorldPhaseConfigurationLoader([mode]).Execute();
        Write("""{"phases":[{"name":"Module.Update","execution":"custom","action":"Update"}]}""");
        Configure();

        World().Execute();

        Assert.Equal(1, executions);
    }
}
