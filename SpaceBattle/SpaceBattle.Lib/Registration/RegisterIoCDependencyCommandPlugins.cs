using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyCommandPlugins(
    IReadOnlyDictionary<string, ICommandPlugin> plugins,
    IReadOnlyDictionary<string, string> targets) : ICommand
{
    public void Execute()
    {
        var resolver = new TargetItemResolver(targets);
        foreach (var (name, plugin) in plugins)
        {
            IoCRegistrar.Register(MessageDependencies.CommandKey(name), args =>
            {
                if (args is not [IAuthenticatedMessage message])
                    throw new UnauthorizedAccessException("An authenticated player is required.");

                var itemId = resolver.Resolve(message);
                var item = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get(itemId);
                return plugin.Create(item, message.Args);
            });
        }
    }
}
