namespace SpaceBattle.Lib;

public sealed class GameSnapshotService(IServer server, IManagedGameRegistry registry, SemaphoreSlim lifecycle,
    Func<string, IManagedGame> restore, IGameStarter starter) : IServerSnapshots
{
    public async Task<IReadOnlyDictionary<string, string>> SnapshotAndStop(ISnapshotStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        await lifecycle.WaitAsync().ConfigureAwait(false);
        var games = registry.Games.Where(game => game.State == GameHostState.Running).ToArray();
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
            if (server.IsRunning)
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
            if (games.Select(game => game.Id).Distinct().Count() != games.Length || games.Any(game => registry.Contains(game.Id)))
                throw new ArgumentException("The snapshot contains an existing game.");
            var started = games.Select(starter.StartCore).ToArray();
            await Task.WhenAll(started.Select(game => game.Execute(() => { }))).ConfigureAwait(false);
        }
        finally { lifecycle.Release(); }
    }
}
