using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal sealed class ConfiguredPlayerGameAccess : IPlayerGameAccess
{
    private readonly IReadOnlyDictionary<string, HashSet<string>> _gamesByPlayer;

    public ConfiguredPlayerGameAccess(IReadOnlyDictionary<string, string[]> gamesByPlayer)
    {
        ArgumentNullException.ThrowIfNull(gamesByPlayer);
        _gamesByPlayer = gamesByPlayer.ToDictionary(pair => pair.Key,
            pair => pair.Value.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    public ValueTask<bool> Allows(string playerId, string gameId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_gamesByPlayer.TryGetValue(playerId, out var games) && games.Contains(gameId));
    }
}
