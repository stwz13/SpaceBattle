using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyRebalancing : ICommand
{
    public void Execute()
    {
        var runtime = Ioc.Resolve<GameRuntime>("Server.Games");
        var ring = Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring);
        runtime.Route = ring.Find;
        RegisterIoCDependencyManagedGames.Register("Server.AddThread", args => args.Length == 0
            ? runtime.AddThread() : throw new ArgumentException("Adding a thread takes no arguments."));
        RegisterIoCDependencyManagedGames.Register("Server.Rebalance", args => args.Length == 0
            ? runtime.Rebalance() : throw new ArgumentException("Rebalancing takes no arguments."));
    }
}
