namespace SpaceBattle.Lib;

public interface IBattleContextFactory
{
    IBattleContext Create(string id);
}
