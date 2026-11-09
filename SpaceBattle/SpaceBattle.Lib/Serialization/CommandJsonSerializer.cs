using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public class CommandJsonSerializer
{
    private readonly IEnumerable<ICommandSerializer> _serializers;

    public CommandJsonSerializer(IEnumerable<ICommandSerializer> serializers)
    {
        ArgumentNullException.ThrowIfNull(serializers);
        _serializers = serializers;
    }

    public JsonObject Write(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var serializer = _serializers.FirstOrDefault(candidate => candidate.Handles(command))
            ?? throw new InvalidOperationException($"A queued command of type {command.GetType().Name} cannot be serialized.");
        var node = serializer.Write(command);
        node["type"] = serializer.Tag;
        return node;
    }

    public ICommand Read(JsonObject node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var tag = node["type"]?.GetValue<string>() ?? throw new FormatException("The serialized command has no 'type' field.");
        return (_serializers.FirstOrDefault(candidate => candidate.Tag == tag)
            ?? throw new FormatException($"Unknown serialized command type '{tag}'.")).Read(node);
    }
}
