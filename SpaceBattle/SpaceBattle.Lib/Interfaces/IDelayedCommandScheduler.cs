namespace SpaceBattle.Lib;

public interface IDelayedCommandScheduler
{
    TimeSpan Now { get; }

    IDisposable ScheduleAt(TimeSpan deadline, ICommand delivery);
}
