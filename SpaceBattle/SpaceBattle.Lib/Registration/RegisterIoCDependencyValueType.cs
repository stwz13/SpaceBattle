using System.Text.Json.Nodes;
using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyValueType<T> : ICommand where T : notnull
{
    private readonly string _tag;
    private readonly Func<T, JsonNode> _write;
    private readonly Func<JsonNode, T> _read;

    public RegisterIoCDependencyValueType(string tag, Func<T, JsonNode> write, Func<JsonNode, T> read)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(read);
        _tag = tag;
        _write = write;
        _read = read;
    }

    public void Execute() => Ioc.Resolve<IValueCodecRegistry>(SerializationDependencies.CodecRegistry).Register(_tag, _write, _read);
}
