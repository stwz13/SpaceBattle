namespace SpaceBattle.Lib;

public sealed class GameTicks : IGameTicks
{
    private readonly object _gate = new();
    private readonly IDelayedCommandScheduler _scheduler;
    private readonly ICommand _worldStep;
    private readonly Func<IGameTicks, long, long, Action<Exception>, ICommand> _delivery;
    private readonly int _maxCatchUpTicks;
    private IDisposable? _pending;
    private TimeSpan _deadline;
    private long _generation;
    private int _catchUpTicks;
    private bool _executing;
    private bool _isRunning;
    private long _tickNumber;
    private Exception? _failure;

    public GameTicks(IDelayedCommandScheduler scheduler, TimeSpan step, ICommand worldStep,
        Func<IGameTicks, long, long, ICommand> delivery, int maxCatchUpTicks = 2)
        : this(scheduler, step, worldStep, AdaptDelivery(delivery), maxCatchUpTicks)
    {
    }

    internal GameTicks(IDelayedCommandScheduler scheduler, TimeSpan step, ICommand worldStep,
        Func<IGameTicks, long, long, Action<Exception>, ICommand> delivery, int maxCatchUpTicks)
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
    public long TickNumber
    {
        get
        {
            lock (_gate)
                return _tickNumber;
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _isRunning;
        }
    }

    public Exception? Failure
    {
        get
        {
            lock (_gate)
                return _failure;
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_isRunning || _executing)
                return;

            _generation = checked(_generation + 1);
            _catchUpTicks = 0;
            _failure = null;
            _isRunning = true;
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
    }

    public void Stop()
    {
        lock (_gate)
            StopCore();
    }

    public void ExecuteTick(long generation, long number)
    {
        lock (_gate)
        {
            if (!IsCurrent(generation, number) || _executing)
                return;

            CancelPending();
            _executing = true;
        }

        try
        {
            _worldStep.Execute();
        }
        catch (Exception error)
        {
            lock (_gate)
            {
                _executing = false;
                Fail(error);
            }
            throw;
        }

        lock (_gate)
        {
            _executing = false;
            _tickNumber = number;
            if (!_isRunning)
                return;

            try
            {
                AdvanceDeadline();
                ScheduleNext();
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }
    }

    private void AdvanceDeadline()
    {
        _deadline += Step;
        var now = _scheduler.Now;
        if (_deadline > now)
            _catchUpTicks = 0;
        else if (_catchUpTicks < _maxCatchUpTicks)
            _catchUpTicks++;
        else
        {
            _deadline = now + Step;
            _catchUpTicks = 0;
        }
    }

    private void ScheduleNext()
    {
        var generation = _generation;
        var number = checked(_tickNumber + 1);
        var delivery = _delivery(this, generation, number, error => ReportDeliveryFailure(generation, number, error));
        _pending = _scheduler.ScheduleAt(_deadline, delivery);
    }

    private bool IsCurrent(long generation, long number) =>
        _isRunning && generation == _generation && number == _tickNumber + 1;

    private void ReportDeliveryFailure(long generation, long number, Exception error)
    {
        lock (_gate)
        {
            if (IsCurrent(generation, number))
                Fail(error);
        }
    }

    private void CancelPending()
    {
        var pending = _pending;
        _pending = null;
        pending?.Dispose();
    }

    private void StopCore()
    {
        _isRunning = false;
        CancelPending();
    }

    private void Fail(Exception error)
    {
        _failure = error;
        StopCore();
    }

    private static Func<IGameTicks, long, long, Action<Exception>, ICommand> AdaptDelivery(
        Func<IGameTicks, long, long, ICommand> delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        return (ticks, generation, number, _) => delivery(ticks, generation, number);
    }
}
