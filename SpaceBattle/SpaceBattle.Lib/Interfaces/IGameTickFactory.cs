namespace SpaceBattle.Lib;

public interface IGameTickFactory
{
    IGameTicks Create(TimeSpan step, ICommand worldStep, Action<ICommand> postToGame,
        int maxCatchUpTicks = 2);
}
