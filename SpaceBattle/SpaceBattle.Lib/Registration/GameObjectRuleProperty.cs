namespace SpaceBattle.Lib;

public sealed class GameObjectRuleProperty<TSettings>
{
    private readonly string _dependency;
    private readonly Func<TSettings, IGameItem, object> _value;

    private GameObjectRuleProperty(string dependency, Func<TSettings, IGameItem, object> value)
    {
        _dependency = dependency;
        _value = value;
    }

    public static GameObjectRuleProperty<TSettings> For<TView, TValue>(string propertyName,
        Func<TSettings, IGameItem, TValue> value) where TView : class where TValue : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(value);
        var view = typeof(TView);
        var property = InterfaceInspector.Properties(view).SingleOrDefault(candidate => candidate.Name == propertyName);
        if (!view.IsInterface || property is null || !property.CanRead || property.PropertyType != typeof(TValue))
            throw new ArgumentException($"Expected a readable {typeof(TValue).Name} property on {view.Name}.",
                nameof(propertyName));

        return new GameObjectRuleProperty<TSettings>(GeneratedNames.PropertyDependency(view, property),
            (settings, item) => value(settings, item));
    }

    internal void Bind(GameObjectRuleBindings bindings, string kind, TSettings settings, bool legacy) =>
        bindings.Add(_dependency, kind, item => _value(settings, item), legacy);
}
