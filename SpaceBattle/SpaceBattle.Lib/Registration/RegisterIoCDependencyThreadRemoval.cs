using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyThreadRemoval : ICommand
{
    public void Execute()
    {
        var runtime = Ioc.Resolve<GameRuntime>("Server.Games");
        runtime.EvacuateThread = ((Server)runtime.Server).RemoveThread;
        RegisterIoCDependencyManagedGames.Register("Server.RemoveThread", args => args is [string id]
            ? runtime.RemoveThread(id) : throw new ArgumentException("Expected a thread id."));
    }
}
