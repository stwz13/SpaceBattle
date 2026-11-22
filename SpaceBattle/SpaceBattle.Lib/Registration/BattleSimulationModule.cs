using App;

namespace SpaceBattle.Lib;

public sealed class BattleSimulationModule
{
    private const string Progress = "Battle.Progress";
    private readonly IBattleShipClassifier _ships;
    private readonly IProjectileCollisionDetector _collisions;

    public BattleSimulationModule(IBattleShipClassifier ships, IProjectileCollisionDetector collisions)
    {
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(collisions);
        _ships = ships;
        _collisions = collisions;
    }

    public void Configure()
    {
        var serverScope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
        new RegisterIoCDependencyLongOperations().Execute();
        new RegisterIoCDependencyWorldTicks(() =>
            ScopeExecution.InScope(serverScope, () => Ioc.Resolve<string[]>("Tick.Config"))).Execute();
        IoCRegistrar.Register(Progress, _ => (Func<BattleProgress?>)(() => null));
        Ioc.Resolve<IMessageInterpreter>(MessagePipelineDependencies.Decorate,
            (Func<IMessageInterpreter, IMessageInterpreter>)(inner =>
                new BattleMessageInterpreter(inner, ReadProgress)));
    }

    internal IBattleProgressTracker Install(IReadOnlyList<string> participants, BattleProgress? restored,
        IReadOnlyList<SavedMovement> restoredPaths)
    {
        var storage = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage;
        var progress = new BattleProgressTracker(participants, restored);
        var flights = new ProjectileFlights(IsFlyingProjectile, Movement);
        new RegisterIoCDependencyWorldCollisionRules([frame =>
            new CallbackCommand(() =>
            {
                if (IsInProgress(progress.Current))
                    new CheckProjectileHitsCommand(frame, IsFlyingProjectile, _ships, _collisions,
                        (projectile, target) => ProjectileHit(storage, flights.Stop, projectile, target)).Execute();
            })]).Execute();
        var segments = Ioc.Resolve<WorldMovementSegments>(WorldCollisionDependencies.MovementSegments);
        var history = new BattleMovementHistory(storage, segments, restoredPaths);

        Action stop = () => { };
        RegisterPhase(WorldCollisionDependencies.Before, items => new CallbackCommand(() =>
        {
            history.Restore();
            progress.Observe(items.Where(_ships.IsShip));
            if (IsInProgress(progress.Current))
                flights.Update(items);
        }));
        RegisterPhase(WorldCollisionDependencies.After, items => new CallbackCommand(() =>
        {
            progress.Observe(items.Where(_ships.IsShip));
            if (progress.Current.IsFinished)
                stop();
        }));
        IoCRegistrar.Register(Progress, _ => (Func<BattleProgress?>)(() => progress.Current));
        IoCRegistrar.Register(BattleMovementHistory.Dependency, _ => history);
        IoCRegistrar.Register(RuntimeServiceDependencies.ActivateGame, _ =>
            (Func<IManagedGameHost, GameActivation>)(host =>
            {
                var owner = host.Thread;
                var runner = host.Runner;
                var receiver = new CommandReceiver(command =>
                {
                    if (!owner.IsStopping)
                        runner.Post(new TransientGameCommand(command));
                });
                new RegisterIoCDependencyLongMovement(receiver, CreateMovementStep, segments.Record).Execute();
                var ticks = Ioc.Resolve<LongOperationCommands>(GameTickDependencies.Create, receiver);
                stop = () =>
                {
                    ticks.Stop.Execute();
                    flights.StopAll();
                };
                return new GameActivation(() =>
                {
                    if (!progress.Current.IsFinished)
                        ticks.Start.Execute();
                }, stop);
            }));
        return progress;
    }

    private static bool IsInProgress(BattleProgress progress) => progress.HasStarted && !progress.IsFinished;

    private static bool IsProjectile(IGameItem item) =>
        BattleItemProperties.Read<string>(item, SerializationDependencies.KindProperty) == "Projectile";

    private static bool IsFlyingProjectile(IGameItem item) => IsProjectile(item) &&
        !BattleItemProperties.IsDestroyed(item) &&
        !string.IsNullOrWhiteSpace(BattleItemProperties.Read<string>(item, "Owner"));

    private static ICommand CreateMovementStep(IGameItem item) => IsProjectile(item)
        ? new MoveCommand(new ProjectileMovementAdapter(item))
        : RegisterIoCDependencyLongMovement.ConfiguredMovement(item);

    private static LongOperationCommands Movement(IGameItem item) =>
        Ioc.Resolve<LongOperationCommands>(MovementDependencies.CreateOperation, item);

    private static ICommand ProjectileHit(IGameItemStorage storage, Action<IGameItem> stopFlight,
        IGameItem projectile, IGameItem target)
    {
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Source"] = projectile, ["Target"] = target });
        return new MacroCommand([
            Ioc.Resolve<ICommand>(CommandContextDependencies.Action, "Hit", context),
            new CallbackCommand(() =>
            {
                target.SetProperty(nameof(IDestructible.Destroyed), true);
                stopFlight(projectile);
                projectile.SetProperty(nameof(IDestructible.Destroyed), true);
                storage.Remove((string)projectile.GetProperty(SerializationDependencies.IdProperty));
            })
        ]);
    }

    private static void RegisterPhase(string key, Func<IReadOnlyCollection<IGameItem>, ICommand> create) =>
        IoCRegistrar.Register(key, args => args is [IReadOnlyCollection<IGameItem> items]
            ? create(items)
            : throw new ArgumentException("A tick phase requires a snapshot of game items."));

    public static BattleProgress? ReadProgress() => Ioc.Resolve<Func<BattleProgress?>>(Progress)();
}
