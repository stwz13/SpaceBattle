using App;
namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyGameTransfer(int bufferCapacity = 10000) : ICommand
{
    public void Execute()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferCapacity, 1);
        var runtime = Ioc.Resolve<GameRuntime>(RuntimeDependencies.Games);
        runtime.Transfers = new GameTransferService(runtime.Server,
            Ioc.Resolve<IManagedGameRegistry>(RuntimeServiceDependencies.Registry),
            Ioc.Resolve<SemaphoreSlim>(RuntimeServiceDependencies.Lifecycle),
            Ioc.Resolve<Func<string, IManagedGame>>(RuntimeServiceDependencies.Restore),
            Ioc.Resolve<Func<IManagedGame, IServerThread?, IManagedGameHost>>(RuntimeServiceDependencies.HostFactory),
            () => new List<IMessage>(), (commit, abort) => new GameArrival(new object(), commit, abort), bufferCapacity, () => runtime);
        IoCRegistrar.Register(TransferDependencies.BufferCapacity, _ => bufferCapacity);
        IoCRegistrar.Register(TransferDependencies.Transfer, args => args is [string id, IRemoteServer target]
            ? runtime.Transfer(id, target) : throw new ArgumentException("Expected game id and destination server."));
        IoCRegistrar.Register(TransferDependencies.Remote, _ => runtime);
    }
}
