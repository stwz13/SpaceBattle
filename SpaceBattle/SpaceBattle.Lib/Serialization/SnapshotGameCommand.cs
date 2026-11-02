using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public class SnapshotGameCommand : ICommand
{
    private readonly IGame _game;
    private readonly Func<IGameItemStorage> _storage;
    private readonly IItemSerializer _items;
    private readonly CommandJsonSerializer _commands;
    private readonly Action<string> _sink;

    public SnapshotGameCommand(IGame game, Func<IGameItemStorage> storage, IItemSerializer items,
        CommandJsonSerializer commands, Action<string> sink)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(sink);
        _game = game;
        _storage = storage;
        _items = items;
        _commands = commands;
        _sink = sink;
    }

    public void Execute() => _sink(new JsonObject
    {
        ["gameId"] = _game.Id,
        ["items"] = new JsonArray(_storage().GetSnapshot().Select(item => (JsonNode?)_items.Write(item)).ToArray()),
        ["commands"] = new JsonArray(_game.Pending.Select(command => (JsonNode?)_commands.Write(command)).ToArray()),
    }.ToJsonString());
}
