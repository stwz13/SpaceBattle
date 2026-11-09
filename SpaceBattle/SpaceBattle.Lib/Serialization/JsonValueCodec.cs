using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public class JsonValueCodec : IValueCodec, IValueCodecRegistry
{
    private readonly ConcurrentDictionary<Type, (string Tag, Func<object, JsonNode> Write)> _writers = new();
    private readonly ConcurrentDictionary<string, Func<JsonNode, object>> _readers = new(StringComparer.Ordinal);

    public void Register<T>(string tag, Func<T, JsonNode> write, Func<JsonNode, T> read) where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(read);
        if (!_writers.TryAdd(typeof(T), (tag, value => write((T)value))))
        {
            throw new InvalidOperationException($"The type {typeof(T).Name} is already registered.");
        }
        if (!_readers.TryAdd(tag, node => read(node)))
        {
            _writers.TryRemove(typeof(T), out _);
            throw new InvalidOperationException($"The tag '{tag}' is already registered.");
        }
    }

    public JsonNode Encode(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _writers.TryGetValue(value.GetType(), out var writer)
            ? new JsonObject { ["type"] = writer.Tag, ["value"] = writer.Write(value) }
            : throw new NotSupportedException($"A value of type {value.GetType().Name} cannot be serialized.");
    }

    public object Decode(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var value = node["value"] ?? throw new FormatException("The serialized value has no 'value' field.");
        var type = node["type"]?.GetValue<string>() ?? throw new FormatException("The serialized value has no 'type' field.");
        return _readers.TryGetValue(type, out var reader)
            ? reader(value)
            : throw new FormatException($"Unknown serialized value type '{type}'.");
    }
}
