namespace SpaceBattle.Lib;

public class GameItemStorage : IGameItemStorage
{
    private readonly Dictionary<string, IGameItem> _items = new(StringComparer.Ordinal);

    public void Add(string id, IGameItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(id, item);
    }

    public IGameItem Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _items[id];
    }

    public bool Remove(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _items.Remove(id);
    }

    public IReadOnlyCollection<IGameItem> GetSnapshot() => Array.AsReadOnly(_items.Values.ToArray());
}
