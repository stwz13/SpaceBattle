namespace SpaceBattle.Lib;

public interface ICommandWhitelist { bool Allows(string name); }

public sealed class CommandWhitelist(IReadOnlySet<string> commands) : ICommandWhitelist
{
    public bool Allows(string name) => commands.Contains(name);
}

public sealed class WhitelistMessageInterpreter(IMessageInterpreter inner, ICommandWhitelist whitelist) : IMessageInterpreter
{
    public ICommand Interpret(IMessage message)
    {
        if (!whitelist.Allows(message.Command)) throw new MessageFormatException("The command is not allowed.");
        return inner.Interpret(message);
    }
}

/// <summary>Performs the same check at ingress so HTTP can reject before acknowledging a message.</summary>
public sealed class WhitelistMessageReceiver(IMessageReceiver inner, IMessageParser parser, ICommandWhitelist whitelist) : IMessageReceiver
{
    public void Receive(string text)
    {
        if (!whitelist.Allows(parser.Parse(text).Command)) throw new MessageFormatException("The command is not allowed.");
        inner.Receive(text);
    }
}
