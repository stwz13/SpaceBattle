namespace SpaceBattle.Lib;

public interface ILoadMeter<TNode>
    where TNode : class
{
    int Count(TNode node, string exceptKey);

    int Total(string exceptKey);
}
