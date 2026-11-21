namespace SpaceBattle.Lib;

public interface IBattleProgressTracker
{
    BattleProgress Current { get; }
    void Observe(IEnumerable<IGameItem> ships);
}
