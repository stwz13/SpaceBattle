namespace SpaceBattle.Lib;

public sealed class GameLifecycle(IServerAvailability server, IManagedGameRegistry games, SemaphoreSlim lifecycle,
    Func<string, IManagedGame> create, Func<IManagedGame, IServerThread?, IManagedGameHost> hostFactory)
    : IGameLifecycle, IGameStarter
{
    public IManagedGameHost Start(string id) => Start(create(id));
    public IManagedGameHost Start(IManagedGame game)
    {
        if (!lifecycle.Wait(0)) throw new ServerOverloadedException("Server management is in progress.");
        try { return StartCore(game); }
        finally { lifecycle.Release(); }
    }
    public IManagedGameHost StartCore(IManagedGame game)
    {
        server.EnsureRunning();
        var host = hostFactory(game, null);
        games.Add(game.Id, host);
        _ = host.Wake();
        return host;
    }
}
