using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public interface IItemSerializer
{
    JsonObject Write(IGameItem item);
    IReadOnlyCollection<KeyValuePair<string, object>> Read(JsonObject node);
}
