using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public interface IValueCodec
{
    JsonNode Encode(object value);
    object Decode(JsonNode node);
}
