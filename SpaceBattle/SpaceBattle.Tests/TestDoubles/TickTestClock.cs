namespace SpaceBattle.Tests;

internal sealed class TickTestClock : TimeProvider
{
    private readonly List<TestTimer> _timers = [];
    private long _timestamp;
    public int TimersCreated => _timers.Count;
    public bool TimerCreatedWithFlowSuppressed { get; private set; }
    public TimeSpan? NextDeadline => _timers.Where(timer => timer.Due.HasValue)
        .Select(timer => timer.Due).Order().FirstOrDefault();
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _timestamp;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        TimerCreatedWithFlowSuppressed = ExecutionContext.IsFlowSuppressed();
        var timer = new TestTimer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        _timestamp += elapsed.Ticks;
    }

    public void FireDue()
    {
        foreach (var timer in _timers.Where(timer => timer.Due <= TimeSpan.FromTicks(_timestamp)).ToArray())
            timer.Fire();
    }

    private sealed class TestTimer(TickTestClock clock, TimerCallback callback, object? state) : ITimer
    {
        private bool _disposed;
        public TimeSpan? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
                return false;
            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : TimeSpan.FromTicks(clock.GetTimestamp()) + dueTime;
            return true;
        }

        public void Fire()
        {
            Due = null;
            if (!_disposed)
                callback(state);
        }

        public void Dispose()
        {
            _disposed = true;
            Due = null;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
