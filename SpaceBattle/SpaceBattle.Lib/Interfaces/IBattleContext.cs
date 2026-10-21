namespace SpaceBattle.Lib;

public interface IBattleContext
{
    string Id { get; }
    IGameItemStorage Storage { get; }
}
