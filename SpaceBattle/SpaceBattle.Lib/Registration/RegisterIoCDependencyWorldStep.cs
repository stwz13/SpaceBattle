using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyWorldStep : ICommand
{
    private readonly string _action;

    public RegisterIoCDependencyWorldStep(string action = WorldStepDependencies.DefaultAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        _action = action;
    }

    public void Execute()
    {
        var factory = new WorldStepFactory(
            () => Ioc.Resolve<IGameItem>(WorldStepDependencies.Frame),
            frame => Ioc.Resolve<ICommand>("Commands.Action", _action, frame));
        Register(WorldStepDependencies.Frame, args =>
        {
            RequireNoArguments(args);
            var battle = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context);
            var items = Ioc.Resolve<IGameItemFactory>(GameItemFactoryDependencies.Factory);
            return items.Create(new Dictionary<string, object>
            {
                [WorldStepDependencies.BattleProperty] = battle,
                [WorldStepDependencies.ItemsProperty] = battle.Storage.GetSnapshot()
            });
        });
        Register(WorldStepDependencies.Factory, args =>
        {
            RequireNoArguments(args);
            return factory;
        });
        Register(WorldStepDependencies.Command, args =>
        {
            RequireNoArguments(args);
            return new WorldStepCommand(Ioc.Resolve<IWorldStepFactory>(WorldStepDependencies.Factory));
        });
    }

    private static void RequireNoArguments(object[] args)
    {
        if (args.Length != 0)
            throw new ArgumentException("World step dependencies take no arguments.");
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
