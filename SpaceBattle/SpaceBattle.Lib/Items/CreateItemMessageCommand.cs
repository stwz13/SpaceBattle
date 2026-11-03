using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public class CreateItemMessageCommand : ICommand
{
    private readonly IMessage _message;
    private readonly IValueCodec _codec;
    private readonly IItemCreator _creator;

    public CreateItemMessageCommand(IMessage message, IValueCodec codec, IItemCreator creator)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(creator);
        _message = message;
        _codec = codec;
        _creator = creator;
    }

    public void Execute()
    {
        var args = JsonNode.Parse(_message.Args.GetRawText())?.AsObject()
            ?? throw new MessageFormatException("The item description must be an object.");
        var properties = (args["properties"]?.AsObject() ?? [])
            .Select(pair => KeyValuePair.Create(pair.Key, _codec.Decode(pair.Value!)));
        new CreateItemCommand(_creator, Field(args, "kind"), Field(args, "id"), properties).Execute();
    }

    private static string Field(JsonObject args, string name) => args[name]?.GetValue<string>()
        ?? throw new MessageFormatException($"The item description has no '{name}' field.");
}
