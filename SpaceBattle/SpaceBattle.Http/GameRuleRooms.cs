using System.Collections.Concurrent;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

public sealed class GameRuleRooms(GameRuleParameterCatalog parameters) : IGameRuleRooms
{
    private readonly ConcurrentDictionary<string, GameRulesLobby> _rooms = new(StringComparer.Ordinal);
    private AgreedGameStarter? _starter;

    internal void Bind(AgreedGameStarter starter) => _starter = starter;

    public void Create(string id, string player, string[] participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (!participants.Contains(player, StringComparer.Ordinal))
            throw new UnauthorizedAccessException("The proposing player must be a participant.");
        var room = new GameRulesLobby(id, participants, parameters);
        if (!_rooms.TryAdd(id, room)) throw new InvalidOperationException("The lobby already exists.");
    }

    public IGameRulesLobby Find(string id) => GetRoom(id);

    private GameRulesLobby GetRoom(string id) => _rooms.TryGetValue(id, out var room)
        ? room : throw new KeyNotFoundException("Unknown lobby.");

    public void Start(string id, string player, long revision) =>
        (_starter ?? throw new InvalidOperationException("Game actions are not configured.")).Start(GetRoom(id), player, revision);
}
