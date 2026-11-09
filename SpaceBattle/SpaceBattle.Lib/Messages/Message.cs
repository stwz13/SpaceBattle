using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class Message : IMessage
{
    public Message(string gameId, string command, JsonElement args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        GameId = gameId;
        Command = command;
        Args = args;
    }

    public string GameId { get; }

    public string Command { get; }

    public JsonElement Args { get; }
}
