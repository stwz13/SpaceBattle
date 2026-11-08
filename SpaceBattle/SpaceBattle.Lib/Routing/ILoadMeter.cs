namespace SpaceBattle.Lib;

/// <summary>Reports how many keys are currently placed on nodes. The key being placed is never counted.</summary>
public interface ILoadMeter<TNode>
    where TNode : class
{
    int Count(TNode node, string exceptKey);

    int Total(string exceptKey);
}
