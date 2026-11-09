using App;
namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyThreadRemoval : ICommand
{
    public void Execute()
    {
        var runtime = Ioc.Resolve<GameRuntime>(RuntimeDependencies.Games);
        runtime.ThreadRemoval = new ThreadRemovalService(runtime.Server, runtime.Rebalancing,
            Ioc.Resolve<SemaphoreSlim>(RuntimeServiceDependencies.Lifecycle));
        IoCRegistrar.Register(RuntimeDependencies.RemoveThread, args => args is [string id]
            ? runtime.RemoveThread(id) : throw new ArgumentException("Expected a thread id."));
    }
}
