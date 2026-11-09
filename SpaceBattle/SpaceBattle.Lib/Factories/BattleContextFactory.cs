namespace SpaceBattle.Lib;

public class BattleContextFactory : IBattleContextFactory
{
    public IBattleContext Create(string id) => new BattleContext(id, new GameItemStorage());
}
