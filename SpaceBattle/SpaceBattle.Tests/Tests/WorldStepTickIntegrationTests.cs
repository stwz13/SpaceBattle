using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class WorldStepTickIntegrationTests
{
    public WorldStepTickIntegrationTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionComposition().Execute();
        new RegisterIoCDependencyWorldStep().Execute();
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    [Fact]
    public void Real_Tick_Chain_Updates_Configured_Objects_And_Stops_Without_Changing_Ticks()
    {
        var time = new TickTestClock();
        var server = new Queue<Lib.ICommand>();
        var game = new Queue<Lib.ICommand>();
        var errors = new List<Exception>();
        using var scheduler = new DelayedCommandScheduler(time, server.Enqueue, errors.Add);
        var battle = new BattleContext("Battle", new GameItemStorage());
        new RegisterIoCDependencyBattleContext(battle).Execute();
        var item = new GameItemFactory().Create();
        item.SetProperty("Position", new Vector(0, 0));
        item.SetProperty("Velocity", new Vector(2, -1));
        battle.Storage.Add("item", item);
        var frames = new List<IGameItem>();
        Register("WorldStep.Config", _ => new[] { "Phase.Record", "Phase.Move", "Phase.Stop" });
        Register("Movement.Config", _ => new[] { "Item.Move" });
        Register("Phase.Record", args => new TickTestCommand(() => frames.Add((IGameItem)args[0])));
        Register("Item.Move", args => new TickTestCommand(() =>
        {
            var target = (IGameItem)args[0];
            target.SetProperty("Position", (Vector)target.GetProperty("Position") + (Vector)target.GetProperty("Velocity"));
        }));
        new RegisterIoCDependencyWorldItemsAction("Phase.Move", "Movement").Execute();
        IGameTicks? ticks = null;
        Register("Phase.Stop", _ => new TickTestCommand(() =>
        {
            if (frames.Count == 2)
                ticks!.Stop();
        }));
        new RegisterIoCDependencyGameTicks(scheduler).Execute();
        ticks = Ioc.Resolve<IGameTicks>(GameTickDependencies.Create, TimeSpan.FromMilliseconds(50),
            Ioc.Resolve<Lib.ICommand>(WorldStepDependencies.Command), (Action<Lib.ICommand>)game.Enqueue);
        Ioc.Resolve<Lib.ICommand>(GameTickDependencies.Start, ticks).Execute();

        foreach (var _ in Enumerable.Range(0, 2))
        {
            time.Advance(TimeSpan.FromMilliseconds(50));
            time.FireDue();
            server.Dequeue().Execute();
            game.Dequeue().Execute();
        }

        Assert.Equal(new Vector(4, -2), item.GetProperty("Position"));
        Assert.Equal(2, ticks.TickNumber);
        Assert.False(ticks.IsRunning);
        Assert.NotSame(frames[0], frames[1]);
        Assert.Empty(server);
        Assert.Empty(game);
        Assert.Empty(errors);
        Assert.Null(time.NextDeadline);
    }

    [Fact]
    public void A_Module_Error_Stops_The_Existing_Tick_Chain()
    {
        var time = new TickTestClock();
        var server = new Queue<Lib.ICommand>();
        var game = new Queue<Lib.ICommand>();
        using var scheduler = new DelayedCommandScheduler(time, server.Enqueue, error => throw error);
        new RegisterIoCDependencyBattleContext(new BattleContext("Battle", new GameItemStorage())).Execute();
        var failure = new InvalidOperationException("update failed");
        Register("WorldStep.Config", _ => new[] { "Phase.Fail" });
        Register("Phase.Fail", _ => new TickTestCommand(() => throw failure));
        var ticks = new GameTickFactory(scheduler).Create(TimeSpan.FromMilliseconds(50),
            Ioc.Resolve<Lib.ICommand>(WorldStepDependencies.Command), game.Enqueue);
        ticks.Start();
        time.Advance(TimeSpan.FromMilliseconds(50));
        time.FireDue();
        server.Dequeue().Execute();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => game.Dequeue().Execute()));

        Assert.Same(failure, ticks.Failure);
        Assert.False(ticks.IsRunning);
        Assert.Equal(0, ticks.TickNumber);
        Assert.Null(time.NextDeadline);
    }
}
