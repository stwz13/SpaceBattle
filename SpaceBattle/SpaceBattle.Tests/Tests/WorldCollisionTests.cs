using App;
using App.Scopes;
using SpaceBattle.Lib;
using LibCommand = SpaceBattle.Lib.ICommand;

namespace SpaceBattle.Tests;

public sealed class WorldCollisionTests
{
    private const string TreeKey = "Collisions.Test";
    private readonly GameScope _scope;
    private readonly GameItemStorage _storage = new();

    public WorldCollisionTests()
    {
        new InitCommand().Execute();
        _scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Create"));
    }

    [Fact]
    public void Configured_Tick_Applies_Hit_On_A_Crossing_And_Does_Not_Replay_The_Path()
    {
        using var scope = _scope.Enter();
        Configure([5, 0, -10, 0]);
        new RegisterIoCDependencyContextActionComposition().Execute();
        new RegisterIoCDependencyCodeGeneration().Execute();
        new RegisterIoCDependencyGeneratedStrategiesWithAdapters([typeof(DestroyIfDeadCommand).Assembly],
            [typeof(IDamageTarget), typeof(IDamageSource)], []).Execute();
        new RegisterIoCDependencyDamageContextCommand("Context.ApplyDamage", "Target", "Source").Execute();
        new RegisterIoCDependencyContextCommand("Context.DestroyIfDead",
            GeneratedNames.CommandDependency(typeof(DestroyIfDeadCommand)), "Target").Execute();
        var ship = AddItem("ship", "target", 5, 0);
        ship.SetProperty("Health", 10);
        ship.SetProperty("Destroyed", false);
        var torpedo = AddItem("torpedo", "source", 0, 0);
        torpedo.SetProperty("Damage", 4);
        torpedo.SetProperty("Velocity", new Vector(0, 0));
        var segments = Segments();
        segments.Record(torpedo, new Vector(10, 0), new Vector(0, 0));
        var tick = Tick();

        tick.Execute();

        Assert.Equal(6, ship.GetProperty("Health"));

        tick.Execute();

        Assert.Equal(6, ship.GetProperty("Health"));
    }

    [Fact]
    public void Tick_Uses_Relative_Movement_Of_Both_Objects_And_Preserves_Their_Roles()
    {
        using var scope = _scope.Enter();
        Configure([6, 3, -10, -5]);
        var hits = CaptureHits();
        var target = AddItem("target", "target", 4, 2);
        var source = AddItem("source", "source", 0, 0);
        var outsider = new TestGameItem();
        outsider.SetProperty("Role", "other");
        _storage.Add("outsider", outsider);
        var start = new Vector(0, 0);
        var end = new Vector(4, 2);
        Segments().Record(target, start, end);
        Segments().Record(source, new Vector(6, 3), new Vector(0, 0));
        start.Coordinates[0] = 20;
        end.Coordinates[1] = 20;
        var tick = Tick();

        tick.Execute();

        var hit = Assert.Single(hits);
        Assert.Same(target, hit.GetItem("Target"));
        Assert.Same(source, hit.GetItem("Source"));

        Segments().Record(target, new Vector(0, 0), new Vector(4, 2));
        Segments().Record(source, new Vector(6, 3), new Vector(10, 5));
        source.SetProperty("Position", new Vector(10, 5));
        tick.Execute();

        Assert.Single(hits);
    }

    [Fact]
    public void Tick_Skips_The_Same_Object_And_Objects_Destroyed_By_Earlier_Hits()
    {
        using var scope = _scope.Enter();
        Configure([0, 0, 0, 0]);
        var hits = CaptureHits(context => context.GetItem("Source").SetProperty("Destroyed", true));
        var both = AddItem("both", "both", 0, 0);
        var other = AddItem("other", "source", 0, 0);
        _storage.Add("alias", other);
        var secondTarget = AddItem("second", "target", 0, 0);
        var destroyed = new TestGameItem();
        destroyed.SetProperty("Destroyed", true);
        _storage.Add("destroyed", destroyed);

        Tick().Execute();

        Assert.Collection(hits,
            hit =>
            {
                Assert.Same(both, hit.GetItem("Target"));
                Assert.Same(other, hit.GetItem("Source"));
            },
            hit =>
            {
                Assert.Same(secondTarget, hit.GetItem("Target"));
                Assert.Same(both, hit.GetItem("Source"));
            });
    }

    [Fact]
    public void Each_Game_Consumes_Its_Own_Segments()
    {
        using var scope = _scope.Enter();
        Configure([5, 0, -10, 0]);
        var hits = CaptureHits();
        AddItem("target", "target", 5, 0);
        var source = AddItem("source", "source", 0, 0);
        var firstGameSegments = Segments();
        firstGameSegments.Record(source, new Vector(10, 0), new Vector(0, 0));

        using (new GameScope(Ioc.Resolve<object>("IoC.Scope.Create")).Enter())
        {
            RegisterPhase();
            Assert.NotSame(firstGameSegments, Segments());
            Tick().Execute();
        }

        Assert.Empty(hits);
        Tick().Execute();
        Assert.Single(hits);
    }

    [Fact]
    public void Segments_Reject_Invalid_Positions_And_A_Second_Step_Before_The_Tick()
    {
        var segments = new WorldMovementSegments();
        var item = new TestGameItem();
        var position = new Vector(0, 0);
        Assert.Throws<ArgumentException>(() => segments.Record(item, position, new Vector(1)));
        segments.Record(item, position, position);
        Assert.Throws<InvalidOperationException>(() => segments.Record(item, position, position));
    }

    [Fact]
    public void Collision_Keys_Validate_Their_Arguments()
    {
        using var scope = _scope.Enter();
        RegisterPhase();

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<LibCommand>(WorldCollisionDependencies.Check));
        Assert.Throws<ArgumentException>(() =>
            Ioc.Resolve<WorldMovementSegments>(WorldCollisionDependencies.MovementSegments, "unexpected"));
    }

    private void Configure(params int[][] vectors)
    {
        new RegisterIoCDependencyBattleContext(new BattleContext("battle", _storage)).Execute();
        new RegisterIoCDependencyMacroCommand().Execute();
        new RegisterIoCDependencyActionConfigurations(Path.Combine(AppContext.BaseDirectory, "Config")).Execute();
        new RegisterIoCDependencyWorldTicks().Execute();
        RegisterPhase();
        Register(TreeKey, _ => new CollisionTree(vectors));
    }

    private static void RegisterPhase() =>
        new RegisterIoCDependencyWorldCollisions(
            item => item.GetProperty("Role") is "target" or "both",
            item => item.GetProperty("Role") is "source" or "both",
            TreeKey, "Hit").Execute();

    private TestGameItem AddItem(string id, string role, int x, int y)
    {
        var item = new TestGameItem();
        item.SetProperty("Role", role);
        item.SetProperty("Position", new Vector(x, y));
        _storage.Add(id, item);
        return item;
    }

    private static List<ICommandContext> CaptureHits(Action<ICommandContext>? onHit = null)
    {
        var hits = new List<ICommandContext>();
        Register(CommandContextDependencies.Action, args =>
        {
            Assert.Equal("Hit", args[0]);
            var context = Assert.IsAssignableFrom<ICommandContext>(args[1]);
            return new DelegateStep(() =>
            {
                hits.Add(context);
                onHit?.Invoke(context);
            });
        });
        return hits;
    }

    private static WorldMovementSegments Segments() =>
        Ioc.Resolve<WorldMovementSegments>(WorldCollisionDependencies.MovementSegments);

    private static LibCommand Tick() => Ioc.Resolve<LibCommand>(GameTickDependencies.Step);

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
