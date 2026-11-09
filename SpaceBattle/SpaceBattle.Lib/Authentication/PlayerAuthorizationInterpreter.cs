using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class PlayerAuthorizationInterpreter(IMessageInterpreter inner,
    Func<IMessage, IMessageInterpreter, ICommand> command) : IMessageInterpreter
{
    public ICommand Interpret(IMessage message) => command(message, inner);
}
