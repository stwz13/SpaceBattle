namespace SpaceBattle.Lib;

public class CommandContext : ICommandContext
{
    private readonly Dictionary<string, IGameItem> _items;

    public CommandContext(IEnumerable<KeyValuePair<string, IGameItem>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = new Dictionary<string, IGameItem>(StringComparer.Ordinal);
        foreach (var (role, item) in items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(role);
            ArgumentNullException.ThrowIfNull(item);
            _items.Add(role, item);
        }
    }

    public IGameItem GetItem(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return _items[role];
    }
}
