namespace SpaceBattle.Lib;

public interface IGameRegistry
{
    void Add(string gameId, IGameHost host);
    bool Remove(string gameId);
    IGameHost Find(string gameId);
}
