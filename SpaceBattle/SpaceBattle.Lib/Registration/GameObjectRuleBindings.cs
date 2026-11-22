using App;

namespace SpaceBattle.Lib;

internal sealed class GameObjectRuleBindings
{
    public const string Dependency = "GameRules.ObjectBindings";
    private readonly Dictionary<string, PropertyRules> _properties = new(StringComparer.Ordinal);

    public void Add(string key, string kind, Func<IGameItem, object> value, bool legacy)
    {
        if (!_properties.TryGetValue(key, out var rules))
        {
            rules = new PropertyRules();
            _properties.Add(key, rules);
            var parent = new GameScope(Ioc.Resolve<object>("IoC.Scope.Parent"));
            IoCRegistrar.Register(key, args =>
            {
                if (args is not [IGameItem item]) throw new ArgumentException("The adapter expects one game item.");
                return rules.Read(item, () => ScopeExecution.InScope(parent, () => Ioc.Resolve<object>(key, args)));
            });
        }

        (legacy ? rules.Legacy : rules.Configured).Add(kind, value);
    }

    private sealed class PropertyRules
    {
        public Dictionary<string, Func<IGameItem, object>> Configured { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Func<IGameItem, object>> Legacy { get; } = new(StringComparer.Ordinal);

        public object Read(IGameItem item, Func<object> fallback)
        {
            var kind = ReadKind(item);
            if (Configured.TryGetValue(kind, out var value) || Legacy.TryGetValue(kind, out value) ||
                Legacy.TryGetValue(string.Empty, out value))
                return value(item);
            return fallback();
        }

        private static string ReadKind(IGameItem item)
        {
            try { return item.GetProperty(SerializationDependencies.KindProperty) as string ?? string.Empty; }
            catch (KeyNotFoundException) { return string.Empty; }
        }
    }
}
