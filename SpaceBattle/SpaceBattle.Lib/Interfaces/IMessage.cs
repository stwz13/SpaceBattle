using System.Text.Json;

namespace SpaceBattle.Lib;

public interface IMessage
{
    string GameId { get; }
    string Command { get; }
    JsonElement Args { get; }
}
