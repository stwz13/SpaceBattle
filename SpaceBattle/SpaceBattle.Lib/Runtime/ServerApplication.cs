namespace SpaceBattle.Lib;

public sealed class ServerOptions
{
    public int ThreadCount { get; set; } = 2;
    public int InboxCapacity { get; set; } = 1024;
    public int RingReplicas { get; set; } = 64;
    public int QuantumMilliseconds { get; set; } = 10;
    public bool PriorityQueues { get; set; } = true;
    public string[] Games { get; set; } = [];
    public string[] AllowedCommands { get; set; } = [];
    public Dictionary<string, string[]> ItemKinds { get; set; } = [];
    public Dictionary<string, string> CommandTargets { get; set; } = [];
    public Dictionary<string, PlayerAccessOptions> Players { get; set; } = [];
    public bool SnapshotOnStop { get; set; }
    public bool RestoreOnStart { get; set; }
    public string SnapshotPath { get; set; } = "data/server-snapshot.json";
    public string JwtIssuer { get; set; } = "SpaceBattle";
    public string JwtAudience { get; set; } = "Players";
    public string JwtSecret { get; set; } = "";
}

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
        runtime.Server.Start();
        try
        {
            if (options.RestoreOnStart) await runtime.Restore(store).ConfigureAwait(false);
            options.Games.Where(id => !runtime.Games.Any(game => game.Id == id)).ToList().ForEach(id => runtime.Start(id));
        }
        catch
        {
            await runtime.Server.SoftStop().ConfigureAwait(false);
            throw;
        }
    }
    public Task Stop() { lock (gate) return _stopping ??= StopCore(); }
    private async Task StopCore()
    {
        if (!runtime.Server.IsRunning) return;
        if (options.SnapshotOnStop) await runtime.SnapshotAndStop(store).ConfigureAwait(false);
        else await runtime.Server.SoftStop().ConfigureAwait(false);
    }
}
