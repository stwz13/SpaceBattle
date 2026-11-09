using App;
namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyRebalancing : ICommand
{
    public void Execute()
    {
        var runtime = Ioc.Resolve<GameRuntime>(RuntimeDependencies.Games);
        runtime.Route = Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring).Find;
        runtime.Rebalancing = new GameRebalanceService(runtime.Server,
            Ioc.Resolve<IManagedGameRegistry>(RuntimeServiceDependencies.Registry),
            Ioc.Resolve<SemaphoreSlim>(RuntimeServiceDependencies.Lifecycle),
            Ioc.Resolve<Func<string, IManagedGame>>(RuntimeServiceDependencies.Restore),
            Ioc.Resolve<Func<IManagedGame, IServerThread?, IManagedGameHost>>(RuntimeServiceDependencies.HostFactory),
            id => runtime.Route(id),
            () => new List<IMessage>(), Ioc.Resolve<int>(TransferDependencies.BufferCapacity));
        IoCRegistrar.Register(RebalanceDependencies.AddThread, args => args.Length == 0
            ? runtime.AddThread() : throw new ArgumentException("Adding a thread takes no arguments."));
        IoCRegistrar.Register(RebalanceDependencies.Rebalance, args => args.Length == 0
            ? runtime.Rebalance() : throw new ArgumentException("Rebalancing takes no arguments."));
    }
}
