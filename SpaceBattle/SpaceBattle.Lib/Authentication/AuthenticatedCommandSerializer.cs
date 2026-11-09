using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public sealed class AuthenticatedCommandSerializer(IMessageInterpreter interpreter,
    Func<IAuthenticatedMessage, JsonObject> write, Func<JsonObject, IAuthenticatedMessage> read,
    Func<IMessageInterpreter, IMessage, ICommand> command) : ICommandSerializer
{
    public string Tag => "authenticated-message";
    public bool Handles(ICommand value) => value is InterpretMessageCommand { Message: IAuthenticatedMessage };
    public JsonObject Write(ICommand value) => value is InterpretMessageCommand { Message: IAuthenticatedMessage message }
        ? write(message) : throw new ArgumentException("Expected an authenticated message.");
    public ICommand Read(JsonObject node) => command(interpreter, read(node));
}
