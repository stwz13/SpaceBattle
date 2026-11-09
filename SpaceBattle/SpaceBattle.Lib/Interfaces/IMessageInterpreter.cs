namespace SpaceBattle.Lib;

public interface IMessageInterpreter
{
    ICommand Interpret(IMessage message);
}
