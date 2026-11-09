namespace SpaceBattle.Lib;

public interface IDelayedCommandScheduler
{
    /// <summary>The current time on the scheduler's monotonic timeline.</summary>
    TimeSpan Now { get; }

    /// <summary>
    /// Schedules delivery on the same timeline as <see cref="Now"/>. Even overdue
    /// commands must be dispatched asynchronously, never on the scheduling call stack.
    /// Dispose the returned handle to cancel dispatch if it has not already started.
    /// If dispatch rejects the command, notify its <see cref="ICommandDeliveryFailureHandler"/>
    /// when implemented. Scheduling and cancellation must not wait for command execution.
    /// </summary>
    IDisposable ScheduleAt(TimeSpan deadline, ICommand delivery);
}
