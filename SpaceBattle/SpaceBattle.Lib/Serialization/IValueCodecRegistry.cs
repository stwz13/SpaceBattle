using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public interface IValueCodecRegistry
{
    void Register<T>(string tag, Func<T, JsonNode> write, Func<JsonNode, T> read) where T : notnull;
}
