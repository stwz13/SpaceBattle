using App;

namespace SpaceBattle.Lib;

public sealed class BattleSimulationModule
{
    private const string Progress = "Battle.Progress";
    private readonly IBattleShipClassifier _ships;
    private readonly IProjectileCollisionDetector _collisions;
    private readonly BattleTickDriver _ticks;

    public BattleSimulationModule(IGameTickFactory factory, IBattleShipClassifier ships, IProjectileCollisionDetector collisions,
        TimeSpan period, Action<Exception> report)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(collisions);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        _ships = ships;
        _collisions = collisions;
        _ticks = new BattleTickDriver(factory, period, report);
    }

    public void Configure()
    {
        IoCRegistrar.Register(Progress, _ => (Func<BattleProgress?>)(() => null));
        Ioc.Resolve<IMessageInterpreter>(MessagePipelineDependencies.Decorate,
            (Func<IMessageInterpreter, IMessageInterpreter>)(inner =>
                new BattleMessageInterpreter(inner, ReadProgress)));
    }

    internal IBattleProgressTracker Install(IReadOnlyList<string> participants, BattleProgress? restored)
    {
        var battle = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context);
        var progress = new BattleProgressTracker(participants, restored);
        var projectiles = new AdvanceBattleProjectilesCommand(battle.Storage, _ships, _collisions,
            context => Ioc.Resolve<ICommand>(CommandContextDependencies.Action, "Hit", context));
        var step = new BattleWorldStepCommand(battle, _ships, projectiles, progress);
        IoCRegistrar.Register(Progress, _ => (Func<BattleProgress?>)(() => progress.Current));
        IoCRegistrar.Register(RuntimeServiceDependencies.ActivateGame, _ =>
            (Func<IManagedGameHost, Action>)(host => _ticks.Attach(host, step, () => progress.Current.IsFinished)));
        return progress;
    }

    public static BattleProgress? ReadProgress() => Ioc.Resolve<Func<BattleProgress?>>(Progress)();
}
