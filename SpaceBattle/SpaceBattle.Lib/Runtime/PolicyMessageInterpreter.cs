using System.Collections.Concurrent;
namespace SpaceBattle.Lib;

public sealed class PolicyMessageInterpreter(IMessageInterpreter inner, Func<IMessage, Action, ICommand> protect) : IMessageInterpreter
{
    public ICommand Interpret(IMessage message) => protect(message, () => inner.Interpret(message).Execute());
}
