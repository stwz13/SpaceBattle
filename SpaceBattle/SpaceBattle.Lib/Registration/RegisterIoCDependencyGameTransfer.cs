using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyGameTransfer(int bufferCapacity = 10000) : ICommand
{
    public void Execute()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferCapacity, 1);
        var runtime = Ioc.Resolve<GameRuntime>("Server.Games");
        runtime.TransferBuffer = () => new List<IMessage>();
        runtime.ArrivalFactory = (commit, abort) => new GameArrival(new object(), commit, abort);
        runtime.TransferBufferLimit = bufferCapacity;
        RegisterIoCDependencyManagedGames.Register("Games.Transfer", args => args is [string id, IRemoteServer target]
            ? runtime.Transfer(id, target) : throw new ArgumentException("Expected game id and destination server."));
        RegisterIoCDependencyManagedGames.Register("Server.Remote", _ => runtime);
    }
}
