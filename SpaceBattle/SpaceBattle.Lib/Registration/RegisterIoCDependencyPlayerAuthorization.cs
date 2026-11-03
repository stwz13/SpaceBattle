using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyPlayerAuthorization(IReadOnlyDictionary<string, string> commandTargets,
    IReadOnlyDictionary<string, PlayerAccessOptions> players) : ICommand
{
    public void Execute()
    {
        if (commandTargets.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            || players.Any(pair => string.IsNullOrWhiteSpace(pair.Key)))
            throw new ArgumentException("Command, target and player names must be nonempty.");
        var rules = new PlayerCommandRules(commandTargets.ToDictionary(), players.ToDictionary(pair => pair.Key,
            pair => ((IReadOnlySet<string>)pair.Value.Commands.ToHashSet(StringComparer.Ordinal),
                (IReadOnlySet<string>)pair.Value.Items.ToHashSet(StringComparer.Ordinal))));
        RegisterIoCDependencyManagedGames.Register("Auth.CommandRules", _ => rules);
        Ioc.Resolve<IMessageInterpreter>("Messages.Decorate", (Func<IMessageInterpreter, IMessageInterpreter>)(inner =>
            new PlayerAuthorizationInterpreter(inner, (message, interpreter) => new PlayerAuthorizationCommand(message,
                interpreter, rules, () => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage))));
    }
}
