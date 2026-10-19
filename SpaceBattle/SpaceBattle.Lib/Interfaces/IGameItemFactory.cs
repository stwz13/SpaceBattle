namespace SpaceBattle.Lib;

public interface IGameItemFactory
{
    IGameItem Create();
    IGameItem Create(IEnumerable<KeyValuePair<string, object>> properties);
}
