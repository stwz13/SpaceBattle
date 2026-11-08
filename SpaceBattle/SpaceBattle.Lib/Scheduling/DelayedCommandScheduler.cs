namespace SpaceBattle.Lib;

public sealed class DelayedCommandScheduler : IDelayedCommandScheduler, IDisposable
{
    private static readonly TimeSpan MaximumTimerDelay = TimeSpan.FromMilliseconds(0xfffffffe);
    private readonly object _gate = new();
    private readonly SortedSet<Delivery> _pending = new(Comparer<Delivery>.Create((a, b) =>
    {
        var deadline = a.Deadline.CompareTo(b.Deadline);
        return deadline != 0 ? deadline : a.Sequence.CompareTo(b.Sequence);
    }));
    private readonly TimeProvider _time;
    private readonly long _origin;
    private readonly ITimer _timer;
    private readonly Action<ICommand> _dispatch;
    private readonly Action<Exception> _onDeliveryError;
    private long _sequence;
    private volatile bool _disposed;

    public DelayedCommandScheduler(TimeProvider time, Action<ICommand> dispatch,
        Action<Exception> onDeliveryError)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(onDeliveryError);
        _time = time;
        _origin = time.GetTimestamp();
        _dispatch = dispatch;
        _onDeliveryError = onDeliveryError;
        if (ExecutionContext.IsFlowSuppressed())
            _timer = CreateTimer();
        else
        {
            using var suppression = ExecutionContext.SuppressFlow();
            _timer = CreateTimer();
        }
    }

    public TimeSpan Now => _time.GetElapsedTime(_origin);

    public IDisposable ScheduleAt(TimeSpan deadline, ICommand delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var entry = new Delivery(this, deadline, checked(++_sequence), delivery);
            _pending.Add(entry);
            try
            {
                Arm();
                return entry;
            }
            catch
            {
                _pending.Remove(entry);
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var entry in _pending)
                entry.Cancel();
            _pending.Clear();
        }
        _timer.Dispose();
    }

    private ITimer CreateTimer() => _time.CreateTimer(_ => DeliverDue(), null,
        Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private void DeliverDue()
    {
        Delivery[] due;
        lock (_gate)
        {
            if (_disposed)
                return;
            var now = Now;
            due = _pending.TakeWhile(entry => entry.Deadline <= now).ToArray();
            foreach (var entry in due)
                _pending.Remove(entry);
            Arm();
        }

        foreach (var entry in due)
        {
            if (_disposed || !entry.TryClaim())
                continue;
            try
            {
                _dispatch(entry.Command);
            }
            catch (Exception error)
            {
                if (entry.Command is ICommandDeliveryFailureHandler handler)
                    handler.OnDeliveryFailed(error);
                _onDeliveryError(error);
            }
        }
    }

    private void Cancel(Delivery entry)
    {
        entry.Cancel();
        lock (_gate)
        {
            if (!_disposed && _pending.Remove(entry))
                Arm();
        }
    }

    private void Arm()
    {
        var delay = Timeout.InfiniteTimeSpan;
        if (_pending.Count != 0)
        {
            delay = _pending.Min!.Deadline - Now;
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;
            if (delay > MaximumTimerDelay)
                delay = MaximumTimerDelay;
            delay = TimeSpan.FromMilliseconds(Math.Ceiling(delay.TotalMilliseconds));
        }
        _timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private sealed class Delivery(DelayedCommandScheduler owner, TimeSpan deadline,
        long sequence, ICommand command) : IDisposable
    {
        private int _state;
        public TimeSpan Deadline { get; } = deadline;
        public long Sequence { get; } = sequence;
        public ICommand Command { get; } = command;
        public bool TryClaim() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;
        public void Cancel() => Interlocked.CompareExchange(ref _state, 2, 0);
        public void Dispose() => owner.Cancel(this);
    }
}
