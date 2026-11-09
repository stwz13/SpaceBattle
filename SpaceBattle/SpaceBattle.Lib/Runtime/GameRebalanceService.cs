namespace SpaceBattle.Lib;

public sealed class GameRebalanceService(IServerThreads server, IManagedGameRegistry registry, SemaphoreSlim lifecycle,
    Func<string, IManagedGame> restore, Func<IManagedGame, IServerThread?, IManagedGameHost> hostFactory,
    Func<string, IServerThread> Route, Func<List<IMessage>> TransferBuffer, int TransferBufferLimit) : IGameRebalanceService
{
    public async Task<IServerThread> AddThread()
    {
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            var thread = server.AddThread();
            await RebalanceCore().ConfigureAwait(false);
            return thread;
        }
        finally { lifecycle.Release(); }
    }

    public async Task Rebalance()
    {
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try { await RebalanceCore().ConfigureAwait(false); }
        finally { lifecycle.Release(); }
    }

    public Task RebalanceCore() => Task.WhenAll(registry.Games
        .Where(host => host.State == GameHostState.Running)
        .Select(host => (Host: host, Target: Route(host.Id)))
        .Where(pair => !ReferenceEquals(pair.Host.Thread, pair.Target))
        .Select(pair => Relocate(pair.Host, pair.Target)));

    private async Task Relocate(IManagedGameHost host, IServerThread target)
    {
        try
        {
            var snapshot = await host.BeginTransfer(TransferBuffer(), TransferBufferLimit).ConfigureAwait(false);
            var game = restore(snapshot);
            game.Paused = true;
            host.Relocate(hostFactory(game, target));
        }
        catch
        {
            if (host.State == GameHostState.Moving) await host.CancelTransfer().ConfigureAwait(false);
            throw;
        }
    }
}
