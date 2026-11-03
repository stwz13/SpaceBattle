using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public class ItemSerializer : IItemSerializer
{
    private readonly IValueCodec _codec;
    private readonly IItemSchema _schema;

    public ItemSerializer(IValueCodec codec, IItemSchema schema)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(schema);
        _codec = codec;
        _schema = schema;
    }

    public JsonObject Write(IGameItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var kind = (string)item.GetProperty(SerializationDependencies.KindProperty);
        return new JsonObject
        {
            ["id"] = (string)item.GetProperty(SerializationDependencies.IdProperty),
            ["kind"] = kind,
            ["properties"] = new JsonObject(_schema.PropertiesOf(kind).Select(name =>
                KeyValuePair.Create(name, (JsonNode?)_codec.Encode(item.GetProperty(name))))),
        };
    }

    public IReadOnlyCollection<KeyValuePair<string, object>> Read(JsonObject node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node["properties"]!.AsObject()
            .Select(pair => KeyValuePair.Create(pair.Key, _codec.Decode(pair.Value!)))
            .Append(KeyValuePair.Create(SerializationDependencies.IdProperty, (object)node["id"]!.GetValue<string>()))
            .Append(KeyValuePair.Create(SerializationDependencies.KindProperty, (object)node["kind"]!.GetValue<string>()))
            .ToArray();
    }
}
