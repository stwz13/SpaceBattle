using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

public sealed partial class GameRuntime(
    IServer server, ConcurrentDictionary<string, IGameHost> hosts, SemaphoreSlim lifecycle,
    Func<string, IManagedGame> create, Func<string, IManagedGame> restore,
    Func<IManagedGame, IServerThread?, IManagedGameHost> hostFactory) : IGameRuntime
{
    public IServer Server => server;
    public IReadOnlyCollection<IManagedGameHost> Games => hosts.Values.OfType<IManagedGameHost>().ToArray();
    public IGameHost Find(string id) => hosts.TryGetValue(id, out var host)
        ? host : throw new KeyNotFoundException($"Game '{id}' was not found.");
    public void Add(string id, IGameHost host)
    {
        if (!hosts.TryAdd(id, host)) throw new ArgumentException($"Game '{id}' already exists.");
    }
    public bool Remove(string id) => hosts.TryRemove(id, out _);

    public IManagedGameHost Start(string id) => Start(create(id));
    public IManagedGameHost Start(IManagedGame game)
    {
        if (!lifecycle.Wait(0)) throw new ServerOverloadedException("Server management is in progress.");
        try { return StartCore(game); }
        finally { lifecycle.Release(); }
    }

    private IManagedGameHost StartCore(IManagedGame game)
    {
        if (server.State != ServerState.Running) throw new InvalidOperationException("The server is not running.");
        var host = hostFactory(game, null);
        Add(game.Id, host);
        _ = host.Wake();
        return host;
    }

    public async Task<IReadOnlyDictionary<string, string>> SnapshotAndStop(ISnapshotStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        await lifecycle.WaitAsync().ConfigureAwait(false);
        var games = Games.Where(game => game.State == GameHostState.Running).ToArray();
        try
        {
            var snapshots = await Task.WhenAll(games.Select(game => game.PauseAndSnapshot())).ConfigureAwait(false);
            var result = games.Select((game, index) => KeyValuePair.Create(game.Id, snapshots[index])).ToDictionary();
            if (store is ISnapshotReplacementStore replacement) replacement.ReplaceAll(result);
            else
            result.ToList().ForEach(pair => store.Save(pair.Key, pair.Value));
            await server.SoftStop().ConfigureAwait(false);
            Array.ForEach(games, game => game.MarkStopped());
            return result;
        }
        catch
        {
            if (server.State == ServerState.Running)
                await Task.WhenAll(games.Where(game => game.State == GameHostState.Moving).Select(game => game.Resume())).ConfigureAwait(false);
            throw;
        }
        finally { lifecycle.Release(); }
    }

    public async Task Restore(ISnapshotStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            var games = store.GameIds.Select(id => restore(store.Load(id))).ToArray();
            if (games.Select(game => game.Id).Distinct().Count() != games.Length || games.Any(game => hosts.ContainsKey(game.Id)))
                throw new ArgumentException("The snapshot contains an existing game.");
            var started = games.Select(StartCore).ToArray();
            await Task.WhenAll(started.Select(game => game.Execute(() => { }))).ConfigureAwait(false);
        }
        finally { lifecycle.Release(); }
    }
}
