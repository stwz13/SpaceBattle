using App;

namespace SpaceBattle.Lib;

internal static class TransactionScopeBindings
{
    internal const string CurrentScope = "IoC.Scope.Current";
    private const string ParentScope = "IoC.Scope.Parent";

    public static IEnumerable<IReadOnlyDictionary<string, Func<object[], object>>> Inherited()
    {
        var current = Ioc.Resolve<object>(CurrentScope);
        var root = ScopeExecution.InScope(new GameScope(current), () =>
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Clear").Execute();
            return Ioc.Resolve<object>(CurrentScope);
        });
        return Enumerate(current, root);
    }

    private static IEnumerable<IReadOnlyDictionary<string, Func<object[], object>>> Enumerate(object scope, object root)
    {
        // App.Scopes exposes its strategy dictionary as the current scope.
        var bindings = (IReadOnlyDictionary<string, Func<object[], object>>)scope;
        yield return bindings;
        if (!ReferenceEquals(scope, root))
        {
            foreach (var parent in Enumerate(bindings[ParentScope]([]), root))
                yield return parent;
        }
    }
}