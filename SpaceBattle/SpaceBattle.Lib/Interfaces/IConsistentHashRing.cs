namespace SpaceBattle.Lib;

public interface IConsistentHashRing<TNode>
    where TNode : class
{
    int Count { get; }
    void Add(string id, TNode node);
    bool Remove(string id);
    TNode Find(string key);
}
