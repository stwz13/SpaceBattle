namespace SpaceBattle.Lib;

internal sealed class ShipDefaultsGameItemFactory(IGameItemFactory inner) : IGameItemFactory
{
    internal const string ShipKind = "Ship";
    internal const string DestroyedProperty = nameof(IDestructible.Destroyed);
    private readonly IGameItemFactory _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IGameItem Create() => _inner.Create();

    public IGameItem Create(IEnumerable<KeyValuePair<string, object>> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var values = properties.ToArray();
        if (!values.Any(property => property.Key == SerializationDependencies.KindProperty && property.Value is ShipKind)
            || values.Any(property => property.Key == DestroyedProperty))
            return _inner.Create(values);

        return _inner.Create(values.Append(KeyValuePair.Create(DestroyedProperty, (object)false)));
    }
}