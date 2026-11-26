using App;

namespace SpaceBattle.Lib;

internal sealed class ActionTransactionScope(IGameScope game,
    IReadOnlyDictionary<string, ITransactionCommandEffect> effects, Action stopGame)
{
    private readonly TransactionJournal _journal = new();
    private object? _activeScope;

    public void Execute(Action execute)
    {
        if (ReferenceEquals(_activeScope, Ioc.Resolve<object>(TransactionScopeBindings.CurrentScope)))
        {
            _journal.Execute(execute, stopGame);
            return;
        }

        using var enteredGame = game.Enter();
        var bindings = TransactionScopeBindings.Inherited();
        var previous = _activeScope;
        var scope = Ioc.Resolve<object>("IoC.Scope.Create");
        using var enteredTransaction = new GameScope(scope).Enter();
        TransactionalPropertyWrites.Register(bindings.SelectMany(dependencies => dependencies.Keys), game, _journal);
        foreach (var (key, effect) in effects)
            IoCRegistrar.Register(key, args => effect.Decorate(args,
                ScopeExecution.InScope(game, () => Ioc.Resolve<ICommand>(key, args)), _journal));

        _activeScope = scope;
        try { _journal.Execute(execute, stopGame); }
        finally { _activeScope = previous; }
    }
}