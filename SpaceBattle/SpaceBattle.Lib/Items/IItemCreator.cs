namespace SpaceBattle.Lib;

public interface IItemCreator
{
    IGameItem Create(string kind, string id, IEnumerable<KeyValuePair<string, object>> properties);
}
