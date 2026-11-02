using App;

namespace SpaceBattle.Lib;

public class IoCMessageInterpreter : IMessageInterpreter
{
    public ICommand Interpret(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Ioc.Resolve<ICommand>(MessageDependencies.CommandKey(message.Command), message);
    }
}
