namespace SpaceBattle.Lib;

public interface IGameTickReceiver
{
    void ExecuteTick(long generation, long number);
}
