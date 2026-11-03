namespace SpaceBattle.Lib;

public class ItemCreator : IItemCreator
{
    private readonly IGameItemFactory _factory;
    private readonly Func<IGameItemStorage> _storage;

    public ItemCreator(IGameItemFactory factory, Func<IGameItemStorage> storage)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(storage);
        _factory = factory;
        _storage = storage;
    }

    public IGameItem Create(string kind, string id, IEnumerable<KeyValuePair<string, object>> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(properties);
        var given = properties.ToArray();
        if (given.Any(pair => pair.Key is SerializationDependencies.IdProperty or SerializationDependencies.KindProperty))
        {
            throw new ArgumentException("The Id and Kind properties are set by the creation itself.");
        }
        var item = _factory.Create(given
            .Append(KeyValuePair.Create(SerializationDependencies.IdProperty, (object)id))
            .Append(KeyValuePair.Create(SerializationDependencies.KindProperty, (object)kind)));
        _storage().Add(id, item);
        return item;
    }
}
