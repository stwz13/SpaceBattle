using System.Text.Json.Nodes;

namespace SpaceBattle.Lib;

public class RestoreItemsCommand : ICommand
{
    private readonly JsonArray _items;
    private readonly Func<IGameItemStorage> _storage;
    private readonly IItemSerializer _serializer;
    private readonly IGameItemFactory _factory;

    public RestoreItemsCommand(JsonArray items, Func<IGameItemStorage> storage, IItemSerializer serializer, IGameItemFactory factory)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(factory);
        _items = items;
        _storage = storage;
        _serializer = serializer;
        _factory = factory;
    }

    public void Execute() => _items.Select(node => node!.AsObject()).ToList().ForEach(node =>
        _storage().Add(node["id"]!.GetValue<string>(), _factory.Create(_serializer.Read(node))));
}
