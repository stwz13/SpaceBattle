using App;

namespace SpaceBattle.Lib;

public sealed class BattleSimulationModule
{
    private const string Progress = "Battle.Progress";
    private const string TickPhases = "Battle.TickPhases";
    private readonly IBattleShipClassifier _ships;
    private readonly IProjectileCollisionDetector _collisions;
    private readonly TimeProvider _time;
    private readonly TimeSpan _period;
    private readonly Action<Exception> _report;

    public BattleSimulationModule(TimeProvider time, IBattleShipClassifier ships, IProjectileCollisionDetector collisions,
        TimeSpan period, Action<Exception> report)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(collisions);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        _time = time;
        _ships = ships;
        _collisions = collisions;
        _period = period;
        _report = report;
    }

    public const string TickConfigurationFile = "BattleTick.config";

    public void Configure(string directory)
    {
        var phases = ActionConfigurationStrategy.Create(Path.Combine(directory, TickConfigurationFile));
        new RegisterIoCDependencyLongOperations().Execute();
        IoCRegistrar.Register(TickPhases, phases);
        IoCRegistrar.Register(Progress, _ => (Func<BattleProgress?>)(() => null));
        Ioc.Resolve<IMessageInterpreter>(MessagePipelineDependencies.Decorate,
            (Func<IMessageInterpreter, IMessageInterpreter>)(inner =>
                new BattleMessageInterpreter(inner, ReadProgress)));
    }

    internal IBattleProgressTracker Install(IReadOnlyList<string> participants, BattleProgress? restored)
    {
        var storage = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage;
        var progress = new BattleProgressTracker(participants, restored);
        new RegisterIoCDependencyWorldTicks(() => Ioc.Resolve<string[]>(TickPhases)).Execute();
        RegisterPhase("Battle.ObserveShips", items =>
            new CallbackCommand(() => progress.Observe(items.Where(_ships.IsShip).ToArray())));
        RegisterPhase("Battle.AdvanceProjectiles", items =>
        {
            var advance = new AdvanceBattleProjectilesCommand(items, _ships, _collisions,
                (projectile, target) => ProjectileHit(storage, projectile, target));
            return new CallbackCommand(() =>
            {
                if (progress.Current.HasStarted && !progress.Current.IsFinished)
                    advance.Execute();
            });
        });
        IoCRegistrar.Register(Progress, _ => (Func<BattleProgress?>)(() => progress.Current));
        IoCRegistrar.Register(RuntimeServiceDependencies.ActivateGame, _ =>
            (Func<IManagedGameHost, GameActivation>)(host =>
            {
                var driver = new BattleTickDriver(host, _time, _period, () => progress.Current.IsFinished,
                    receiver => Ioc.Resolve<LongOperationCommands>(GameTickDependencies.Create, receiver), _report);
                return new GameActivation(driver.Activate, driver.Suspend);
            }));
        return progress;
    }

    private static ICommand ProjectileHit(IGameItemStorage storage, IGameItem projectile, IGameItem target)
    {
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Source"] = projectile, ["Target"] = target });
        return new MacroCommand([
            Ioc.Resolve<ICommand>(CommandContextDependencies.Action, "Hit", context),
            new CallbackCommand(() =>
            {
                target.SetProperty(nameof(IDestructible.Destroyed), true);
                storage.Remove((string)projectile.GetProperty(SerializationDependencies.IdProperty));
            })
        ]);
    }

    private static void RegisterPhase(string key, Func<IReadOnlyCollection<IGameItem>, ICommand> create) =>
        IoCRegistrar.Register(key, args => args is [IReadOnlyCollection<IGameItem> items]
            ? create(items)
            : throw new ArgumentException("A battle phase requires a snapshot of game items."));

    public static BattleProgress? ReadProgress() => Ioc.Resolve<Func<BattleProgress?>>(Progress)();
}
