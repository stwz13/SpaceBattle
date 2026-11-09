using App;
namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyPlayerAuthorization(IReadOnlyDictionary<string, string> commandTargets,
    IReadOnlyDictionary<string, PlayerAccessOptions> players,
    IReadOnlyDictionary<string, IReadOnlyList<IAccessRule>>? configuredRules = null) : ICommand
{
    public void Execute()
    {
        if (commandTargets.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            || players.Any(pair => string.IsNullOrWhiteSpace(pair.Key)))
            throw new ArgumentException("Command, target and player names must be nonempty.");
        var commandRules = commandTargets.Keys.ToDictionary(key => key,
            _ => (IReadOnlyList<IAccessRule>)new IAccessRule[] { new ExistingItemOwnerRule() });
        if (commandRules.ContainsKey(ItemDependencies.CreateCommandKey))
            commandRules[ItemDependencies.CreateCommandKey] = new IAccessRule[] { new NewItemOwnerRule() };
        foreach (var pair in configuredRules ?? new Dictionary<string, IReadOnlyList<IAccessRule>>())
        {
            if (!commandRules.ContainsKey(pair.Key) || pair.Value is null || pair.Value.Count == 0 || pair.Value.Any(rule => rule is null))
                throw new ArgumentException("Every command requires a nonempty list of access rules.");
            commandRules[pair.Key] = pair.Value.ToArray();
        }
        var rules = new PlayerCommandRules(
            new PlayerCommandPermissions(players.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<string>)pair.Value.Commands.ToHashSet(StringComparer.Ordinal))),
            new TargetItemResolver(commandTargets.ToDictionary()),
            new PlayerItemPermissions(players.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<string>)pair.Value.Items.ToHashSet(StringComparer.Ordinal))),
            commandRules);
        var owners = new ScopedItemOwners(() => Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage);
        IoCRegistrar.Register(AuthenticationDependencies.CommandRules, _ => rules);
        Ioc.Resolve<IMessageInterpreter>(MessagePipelineDependencies.Decorate, (Func<IMessageInterpreter, IMessageInterpreter>)(inner =>
            new PlayerAuthorizationInterpreter(inner, (message, interpreter) => new PlayerAuthorizationCommand(message, interpreter, rules, owners))));
    }
}
