namespace SpaceBattle.Lib;

public interface IMessageParser
{
    IMessage Parse(string text);
}
