namespace SpaceBattle.Lib;

public interface IGameItemStorage
{
    void Add(string id, IGameItem item);
    IGameItem Get(string id);
    bool Remove(string id);
    IReadOnlyCollection<IGameItem> GetSnapshot();
}
