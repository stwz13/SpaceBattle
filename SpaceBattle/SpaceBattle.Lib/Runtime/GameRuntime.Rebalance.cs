namespace SpaceBattle.Lib;

public sealed partial class GameRuntime
{
    internal Func<string, IServerThread> Route { get; set; } = null!;

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

    private Task RebalanceCore() => Task.WhenAll(Games
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
