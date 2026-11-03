using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public class MessageCommandSerializer : ICommandSerializer
{
    private readonly IMessageInterpreter _interpreter;

    public MessageCommandSerializer(IMessageInterpreter interpreter)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        _interpreter = interpreter;
    }

    public string Tag => "message";

    public bool Handles(ICommand command) => command is InterpretMessageCommand;

    public JsonObject Write(ICommand command) => command is InterpretMessageCommand interpret
        ? new JsonObject
        {
            ["gameId"] = interpret.Message.GameId,
            ["command"] = interpret.Message.Command,
            ["args"] = JsonNode.Parse(interpret.Message.Args.GetRawText()),
        }
        : throw new ArgumentException($"A command of type {command.GetType().Name} is not a message.", nameof(command));

    public ICommand Read(JsonObject node) => new InterpretMessageCommand(_interpreter, new Message(
        node["gameId"]!.GetValue<string>(),
        node["command"]!.GetValue<string>(),
        JsonSerializer.SerializeToElement(node["args"])));
}
