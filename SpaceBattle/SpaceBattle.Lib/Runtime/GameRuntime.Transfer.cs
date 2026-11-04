namespace SpaceBattle.Lib;

public sealed partial class GameRuntime : IRemoteServer
{
    internal Func<List<IMessage>> TransferBuffer { get; set; } = null!;
    internal Func<Action<IReadOnlyCollection<IMessage>>, Action, IGameArrival> ArrivalFactory { get; set; } = null!;
    internal int TransferBufferLimit { get; set; }
    public void Send(IMessage message) => Find(message.GameId).Send(message);

    public async Task Transfer(string id, IRemoteServer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(this, destination)) throw new ArgumentException("Source and destination must differ.");
        // A server accepts only one topology operation at a time. Reject instead of deadlocking crossed transfers.
        if (!await lifecycle.WaitAsync(0).ConfigureAwait(false)) throw new ServerOverloadedException("Server management is in progress.");
        IManagedGameHost? source = null;
        IGameArrival? arrival = null;
        try
        {
            source = (IManagedGameHost)Find(id);
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
            if (server.State != ServerState.Running) throw new InvalidOperationException("The target server is not running.");
            var game = restore(snapshot);
            if (hosts.ContainsKey(game.Id)) throw new ArgumentException("The target already hosts this game.");
            game.Paused = true;
            var host = hostFactory(game, null);
            return ArrivalFactory(messages =>
            {
                Add(game.Id, host);
                try { host.Activate(messages); }
                catch { Remove(game.Id); throw; }
                lifecycle.Release();
            }, () => lifecycle.Release());
        }
        catch { lifecycle.Release(); throw; }
    }
}
