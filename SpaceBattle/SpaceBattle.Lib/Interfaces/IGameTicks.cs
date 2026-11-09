namespace SpaceBattle.Lib;

public interface IGameTicks : IGameTickControl, IGameTickReceiver
{
    TimeSpan Step { get; }
    long TickNumber { get; }
    bool IsRunning { get; }
    Exception? Failure { get; }
}
