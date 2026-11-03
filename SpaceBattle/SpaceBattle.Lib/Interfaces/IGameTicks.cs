namespace SpaceBattle.Lib;

public interface IGameTicks
{
    TimeSpan Step { get; }
    long TickNumber { get; }
    bool IsRunning { get; }
    Exception? Failure { get; }
    void Start();
    void Stop();
    void ExecuteTick(long generation, long number);
}
