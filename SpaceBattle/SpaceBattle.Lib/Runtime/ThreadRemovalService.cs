namespace SpaceBattle.Lib;

public sealed class ThreadRemovalService(IServerThreadRemoval server, IGameRebalanceService rebalancing,
    SemaphoreSlim lifecycle) : IThreadRemoval
{
    public async Task RemoveThread(string id)
    {
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try { await server.RemoveThread(id, _ => rebalancing.RebalanceCore()).ConfigureAwait(false); }
        finally { lifecycle.Release(); }
    }
}
