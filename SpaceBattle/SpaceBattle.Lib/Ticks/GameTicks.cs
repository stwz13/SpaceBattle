namespace SpaceBattle.Lib;

public sealed class GameTicks : IGameTicks
{
    private readonly IDelayedCommandScheduler _scheduler;
    private readonly ICommand _worldStep;
    private readonly Func<IGameTicks, long, long, ICommand> _delivery;
    private readonly int _maxCatchUpTicks;
    private IDisposable? _pending;
    private TimeSpan _deadline;
    private long _generation;
    private int _catchUpTicks;
    private bool _executing;

    public GameTicks(IDelayedCommandScheduler scheduler, TimeSpan step, ICommand worldStep,
        Func<IGameTicks, long, long, ICommand> delivery, int maxCatchUpTicks = 2)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(worldStep);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(maxCatchUpTicks);
        _scheduler = scheduler;
        _worldStep = worldStep;
        _delivery = delivery;
        Step = step;
        _maxCatchUpTicks = maxCatchUpTicks;
    }

    public TimeSpan Step { get; }
    public long TickNumber { get; private set; }
    public bool IsRunning { get; private set; }
    public Exception? Failure { get; private set; }

    public void Start()
    {
        if (IsRunning || _executing)
            return;

        _generation = checked(_generation + 1);
        _catchUpTicks = 0;
        Failure = null;
        IsRunning = true;
        try
        {
            _deadline = _scheduler.Now + Step;
            ScheduleNext();
        }
        catch (Exception error)
        {
            Fail(error);
            throw;
        }
    }

    public void Stop()
    {
        IsRunning = false;
        _pending?.Dispose();
        _pending = null;
    }

    public void ExecuteTick(long generation, long number)
    {
        if (!IsRunning || _executing || generation != _generation || number != TickNumber + 1)
            return;

        _pending?.Dispose();
        _pending = null;
        _executing = true;
        try
        {
            _worldStep.Execute();
            TickNumber = number;
            if (!IsRunning)
                return;

            _deadline += Step;
            var now = _scheduler.Now;
            if (_deadline <= now)
            {
                if (_catchUpTicks < _maxCatchUpTicks)
                    _catchUpTicks++;
                else
                {
                    _deadline = now + Step;
                    _catchUpTicks = 0;
                }
            }
            else
                _catchUpTicks = 0;

            ScheduleNext();
        }
        catch (Exception error)
        {
            Fail(error);
            throw;
        }
        finally
        {
            _executing = false;
        }
    }

    private void ScheduleNext() => _pending = _scheduler.ScheduleAt(_deadline,
        _delivery(this, _generation, checked(TickNumber + 1)));

    private void Fail(Exception error)
    {
        Failure = error;
        Stop();
    }
}
