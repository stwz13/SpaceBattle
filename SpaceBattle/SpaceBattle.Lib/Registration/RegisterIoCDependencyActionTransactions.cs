using App;

namespace SpaceBattle.Lib;

/// <summary>Decorates both action composers in the current game's scope.</summary>
public sealed class RegisterIoCDependencyActionTransactions : ICommand
{
    private readonly IReadOnlyDictionary<string, ITransactionCommandEffect> _effects;

    public RegisterIoCDependencyActionTransactions(IEnumerable<ITransactionCommandEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        _effects = effects.ToDictionary(effect => effect.CommandKey, StringComparer.Ordinal);
    }

    public void Execute()
    {
        var game = new GameScope(Ioc.Resolve<object>(TransactionScopeBindings.CurrentScope));
        var battle = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context);
        var stop = new StopGameErrorAction(Ioc.Resolve<IManagedGameRegistry>(RuntimeServiceDependencies.Registry));
        var transaction = new ActionTransactionScope(game, _effects, () => stop.Apply(battle.Id));
        var inherited = TransactionScopeBindings.Inherited().ToArray();
        var composers = new[] { "Commands.Action", CommandContextDependencies.Action }
            .Select(key => (Key: key, Compose: inherited.First(bindings => bindings.ContainsKey(key))[key]))
            .ToArray();
        foreach (var (key, compose) in composers)
            IoCRegistrar.Register(key, args => new CallbackCommand(() =>
                transaction.Execute(() => ((ICommand)compose(args)).Execute())));
    }
}
