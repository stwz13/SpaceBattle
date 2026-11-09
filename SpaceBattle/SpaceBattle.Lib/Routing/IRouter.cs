namespace SpaceBattle.Lib;

public interface IRouter<TNode>
    where TNode : class
{
    TNode Route(string key);
}
