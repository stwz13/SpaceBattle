using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using LibCommand = SpaceBattle.Lib.ICommand;

namespace SpaceBattle.Tests;

public sealed class WorldTickTests
{
    private readonly GameScope _scope;

    public WorldTickTests()
    {
        new InitCommand().Execute();
        _scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Create"));
    }

    [Fact]
    public void Tick_Requeues_Behind_Other_Commands_And_Stops_Through_The_Game_Queue()
    {
        using var scope = _scope.Enter();
        var runs = new List<string>();
        Configure(new GameItemStorage());
        Register("Tick.Config", _ => new[] { "World.Audit" });
        Register("World.Audit", _ => new DelegateStep(() => runs.Add("tick")));
        var game = new Game("battle", new GameQueue(), _scope, Mock.Of<IQuantum>(quantum => quantum.IsOver));
        var ticks = Ioc.Resolve<LongOperationCommands>(GameTickDependencies.Create, new CommandReceiver(game.Post));

        game.Post(ticks.Start);
        game.Execute();
        var continuation = Assert.Single(game.Pending);
        game.Post(new DelegateStep(() => runs.Add("other")));
        game.Execute();
        Assert.Same(continuation, game.Pending.Last());
        game.Post(ticks.Stop);
        game.Execute();
        game.Execute();
        game.Execute();
        game.Execute();

        Assert.Equal(new[] { "tick", "other", "tick" }, runs);
        Assert.False(game.HasCommands);
    }

    [Fact]
    public void Tick_Reloads_Phase_Order_And_Shares_One_Fresh_Snapshot_Per_Step()
    {
        using var scope = _scope.Enter();
        var storage = new GameItemStorage();
        var observed = new List<(string Phase, IReadOnlyCollection<IGameItem> Items)>();
        var config = Directory.CreateTempSubdirectory("SpaceBattle-Tick-Config-");
        try
        {
            var tickConfig = Path.Combine(config.FullName, "Tick.config");
            File.WriteAllLines(tickConfig, ["World.First", "World.Second"]);
            Configure(storage);
            new RegisterIoCDependencyActionConfigurations(config.FullName).Execute();
            Register("World.First", args => Phase("first", args));
            Register("World.Second", args => Phase("second", args));
            var step = Ioc.Resolve<LibCommand>(GameTickDependencies.Step);
            var ship = new TestGameItem();

            step.Execute();
            storage.Add("ship", ship);
            File.WriteAllLines(tickConfig, ["World.Second", "World.First"]);
            step.Execute();
            File.WriteAllText(tickConfig, string.Empty);
            step.Execute();

            Assert.Equal(new[] { "first", "second", "second", "first" }, observed.Select(entry => entry.Phase));
            Assert.Empty(observed[0].Items);
            Assert.Same(observed[0].Items, observed[1].Items);
            Assert.Same(ship, Assert.Single(observed[2].Items));
            Assert.Same(observed[2].Items, observed[3].Items);
        }
        finally
        {
            config.Delete(true);
        }

        LibCommand Phase(string name, object[] args) => new DelegateStep(() =>
            observed.Add((name, Assert.IsAssignableFrom<IReadOnlyCollection<IGameItem>>(Assert.Single(args)))));
    }

    [Fact]
    public void Tick_Uses_The_Battle_Context_From_Its_Game_Scope()
    {
        using var scope = _scope.Enter();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyWorldTicks().Execute();
        var observed = new List<IGameItem>();
        Register("Tick.Config", _ => new[] { "World.Audit" });
        Register("World.Audit", args => new DelegateStep(() =>
            observed.Add(Assert.Single((IReadOnlyCollection<IGameItem>)Assert.Single(args)))));
        var gameOneItem = new TestGameItem();
        var gameTwoItem = new TestGameItem();

        ExecuteInGame("one", gameOneItem);
        ExecuteInGame("two", gameTwoItem);

        Assert.Equal(new[] { gameOneItem, gameTwoItem }, observed);
    }

    [Fact]
    public void Tick_Registration_Rejects_Incorrect_Arguments()
    {
        using var scope = _scope.Enter();
        Configure(new GameItemStorage());

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<LibCommand>(GameTickDependencies.Step, "unexpected"));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<LongOperationCommands>(GameTickDependencies.Create));
    }

    private static void Configure(IGameItemStorage storage)
    {
        new RegisterIoCDependencyBattleContext(new BattleContext("battle", storage)).Execute();
        new RegisterIoCDependencyLongOperations().Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyWorldTicks().Execute();
    }

    private static void ExecuteInGame(string id, IGameItem item)
    {
        using var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Create")).Enter();
        var storage = new GameItemStorage();
        storage.Add(id, item);
        new RegisterIoCDependencyBattleContext(new BattleContext(id, storage)).Execute();
        Ioc.Resolve<LibCommand>(GameTickDependencies.Step).Execute();
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
