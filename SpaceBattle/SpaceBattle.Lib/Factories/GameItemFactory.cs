namespace SpaceBattle.Lib;

public class GameItemFactory : IGameItemFactory
{
    public IGameItem Create() => Create(Array.Empty<KeyValuePair<string, object>>());

    public IGameItem Create(IEnumerable<KeyValuePair<string, object>> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return new GameItem(new Dictionary<string, object>(properties, StringComparer.Ordinal));
    }
}
