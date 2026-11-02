namespace SpaceBattle.Lib;

public class MessageFormatException : FormatException
{
    public MessageFormatException(string message) : base(message)
    {
    }

    public MessageFormatException(string message, Exception inner) : base(message, inner)
    {
    }
}
