namespace SpaceBattle.Lib;

/// <summary>Chooses the node which should host the given key.</summary>
public interface IRouter<TNode>
    where TNode : class
{
    TNode Route(string key);
}
