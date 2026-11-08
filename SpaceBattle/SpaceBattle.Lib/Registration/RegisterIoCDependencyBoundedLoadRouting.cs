using App;

namespace SpaceBattle.Lib;

/// <summary>Replaces plain ring routing by bounded-load routing. Run it after managed games and rebalancing are registered.</summary>
public sealed class RegisterIoCDependencyBoundedLoadRouting : ICommand
{
    private readonly double _tolerance;
    private readonly int _probes;

    public RegisterIoCDependencyBoundedLoadRouting(double tolerance, int probes = 16)
    {
        if (double.IsNaN(tolerance) || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "The tolerance must not be negative.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(probes, 1);
        _tolerance = tolerance;
        _probes = probes;
    }

    public void Execute()
    {
        var runtime = Ioc.Resolve<GameRuntime>("Server.Games");
        var ring = Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring);
        var moves = new PlannedMoves<IServerThread>();
        var meter = new PlacementLoadMeter<IServerThread>(() => runtime.Games
            .Where(game => game.State is GameHostState.Running or GameHostState.Moving)
            .Select(game => (game.Id, moves.Placement(game.Id, game.Thread, game.State == GameHostState.Moving)))
            .ToArray());
        var router = new BoundedLoadRouter<IServerThread>(ring, meter, _tolerance, _probes);
        RegisterIoCDependencyManagedGames.Register(RoutingDependencies.Thread, args =>
            args.Length == 1 && args[0] is string gameId
                ? router.Route(gameId)
                : throw new ArgumentException("Routing requires the identifier of a game."));
        runtime.Route = new PlanningRouter<IServerThread>(router, moves).Route;
    }
}
