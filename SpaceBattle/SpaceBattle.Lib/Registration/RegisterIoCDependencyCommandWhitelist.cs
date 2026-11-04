using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyCommandWhitelist(IEnumerable<string> names) : ICommand
{
    public void Execute()
    {
        var commands = names.ToHashSet(StringComparer.Ordinal);
        if (commands.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Command names must be nonempty.");
        var whitelist = new CommandWhitelist(commands);
        RegisterIoCDependencyManagedGames.Register("Messages.Whitelist", _ => whitelist);
        Ioc.Resolve<IMessageInterpreter>("Messages.Decorate",
            (Func<IMessageInterpreter, IMessageInterpreter>)(inner => new WhitelistMessageInterpreter(inner, whitelist)));
        var receiver = new WhitelistMessageReceiver(Ioc.Resolve<IMessageReceiver>(MessageDependencies.Receiver),
            Ioc.Resolve<IMessageParser>(MessageDependencies.Parser), whitelist);
        RegisterIoCDependencyManagedGames.Register("Messages.ValidatedReceiver", _ => receiver);
    }
}
