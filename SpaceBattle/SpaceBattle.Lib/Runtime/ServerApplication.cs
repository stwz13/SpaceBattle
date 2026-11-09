namespace SpaceBattle.Lib;

public sealed class ServerApplication(IGameRuntime runtime, IPlayerTokenValidator tokens,
    Func<PlayerIdentity, IMessageReceiver> receiver, ISnapshotStore store, ServerOptions options, object gate) : IServerApplication
{
    private Task? _stopping;
    public IGameRuntime Runtime { get; } = runtime;
    public IPlayerTokenValidator Tokens { get; } = tokens;
    public ISnapshotStore Store { get; } = store;
    public IMessageReceiver Receiver(PlayerIdentity player) => receiver(player);
    public async Task Start()
    {
        Runtime.Server.Start();
        try
        {
            if (options.RestoreOnStart) await Runtime.Restore(Store).ConfigureAwait(false);
            options.Games.Where(id => !Runtime.Games.Any(game => game.Id == id)).ToList().ForEach(id => Runtime.Start(id));
        }
        catch
        {
            await Runtime.Server.SoftStop().ConfigureAwait(false);
            throw;
        }
    }
    public Task Stop() { lock (gate) return _stopping ??= StopCore(); }
    private async Task StopCore()
    {
        if (!Runtime.Server.IsRunning) return;
        if (options.SnapshotOnStop) await Runtime.SnapshotAndStop(Store).ConfigureAwait(false);
        else await Runtime.Server.SoftStop().ConfigureAwait(false);
    }
}
