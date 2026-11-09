namespace SpaceBattle.Lib;

public sealed class GameTransferService(IServerAvailability server, IManagedGameRegistry registry,
    SemaphoreSlim lifecycle, Func<string, IManagedGame> restore,
    Func<IManagedGame, IServerThread?, IManagedGameHost> hostFactory, Func<List<IMessage>> TransferBuffer,
    Func<Action<IReadOnlyCollection<IMessage>>, Action, IGameArrival> ArrivalFactory, int TransferBufferLimit,
    Func<IRemoteServer> Self)
    : IGameTransferService
{
    public void Send(IMessage message) => registry.Find(message.GameId).Send(message);

    public async Task Transfer(string id, IRemoteServer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(Self(), destination)) throw new ArgumentException("Source and destination must differ.");
        // A server accepts only one topology operation at a time. Reject instead of deadlocking crossed transfers.
        if (!await lifecycle.WaitAsync(0).ConfigureAwait(false)) throw new ServerOverloadedException("Server management is in progress.");
        IManagedGameHost? source = null;
        IGameArrival? arrival = null;
        try
        {
            source = registry.FindManaged(id);
            var snapshot = await source.BeginTransfer(TransferBuffer(), TransferBufferLimit).ConfigureAwait(false);
            arrival = await destination.Receive(snapshot).ConfigureAwait(false);
            source.CommitTransfer(arrival, destination);
        }
        catch
        {
            if (arrival?.IsCommitted != true)
            {
                arrival?.Abort();
                if (source?.State == GameHostState.Moving) await source.CancelTransfer().ConfigureAwait(false);
            }
            else if (source is not null) source.CommitTransfer(arrival, destination);
            throw;
        }
        finally { lifecycle.Release(); }
    }

    public async Task<IGameArrival> Receive(string snapshot)
    {
        if (!await lifecycle.WaitAsync(0).ConfigureAwait(false)) throw new ServerOverloadedException("The target is busy.");
        try
        {
            server.EnsureRunning();
            var game = restore(snapshot);
            if (registry.Contains(game.Id)) throw new ArgumentException("The target already hosts this game.");
            game.Paused = true;
            var host = hostFactory(game, null);
            return ArrivalFactory(messages =>
            {
                registry.Add(game.Id, host);
                try { host.Activate(messages); }
                catch { registry.Remove(game.Id); throw; }
                lifecycle.Release();
            }, () => lifecycle.Release());
        }
        catch { lifecycle.Release(); throw; }
    }
}
