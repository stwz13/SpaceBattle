using App;

namespace SpaceBattle.Lib;

internal static class TransactionalPropertyWrites
{
    private const string PropertyPrefix = DependencyNamespaces.Properties + ".";
    private const string SetterSuffix = ".Set";

    public static void Register(IEnumerable<string> inheritedKeys, IGameScope game, ITransactionUndo undo)
    {
        var keys = inheritedKeys.Where(key => key.StartsWith(PropertyPrefix, StringComparison.Ordinal)
            && key.EndsWith(SetterSuffix, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal);
        foreach (var key in keys)
            RegisterSetter(key, game, undo);
    }

    private static void RegisterSetter(string key, IGameScope game, ITransactionUndo undo)
    {
        // GeneratedNames.PropertyDependency ends with the raw property name.
        var propertyKey = key[..^SetterSuffix.Length];
        var property = propertyKey[(propertyKey.LastIndexOf('.') + 1)..];
        IoCRegistrar.Register(key, args =>
        {
            // A getter named Set also ends with ".Set", but receives only its item.
            if (args is not [IGameItem item, _])
                return ScopeExecution.InScope(game, () => Ioc.Resolve<object>(key, args));

            var write = ScopeExecution.InScope(game, () => Ioc.Resolve<ICommand>(key, args));
            return new CallbackCommand(() =>
            {
                var previous = item.GetProperty(property);
                undo.Record(() => item.SetProperty(property, previous));
                write.Execute();
            });
        });
    }
}