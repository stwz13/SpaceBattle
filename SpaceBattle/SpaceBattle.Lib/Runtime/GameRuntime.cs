namespace SpaceBattle.Lib;

public sealed partial class GameRuntime(IServer server, IManagedGameRegistry registry,
    IGameLifecycle games, IServerSnapshots snapshots) : IGameRuntime
{
    public IServer Server => server;
    public IReadOnlyCollection<IManagedGameHost> Games => registry.Games;
    public IGameHost Find(string id) => registry.Find(id);
    public void Add(string id, IGameHost host) => registry.Add(id, host);
    public bool Remove(string id) => registry.Remove(id);
    public IManagedGameHost Start(string id) => games.Start(id);
    public IManagedGameHost Start(IManagedGame game) => games.Start(game);
    public Task<IReadOnlyDictionary<string, string>> SnapshotAndStop(ISnapshotStore store) => snapshots.SnapshotAndStop(store);
    public Task Restore(ISnapshotStore store) => snapshots.Restore(store);
}
