namespace SpaceBattle.Lib;

public sealed class DelegatingMessageInterpreter(Func<IMessageInterpreter> interpreter) : IMessageInterpreter
{
    public ICommand Interpret(IMessage message) => interpreter().Interpret(message);
}
