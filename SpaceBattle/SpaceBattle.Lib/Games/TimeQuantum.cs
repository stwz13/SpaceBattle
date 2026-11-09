namespace SpaceBattle.Lib;

public class TimeQuantum : IQuantum
{
    private readonly TimeSpan _duration;
    private readonly TimeProvider _time;
    private long _start;

    public TimeQuantum(TimeSpan duration, TimeProvider time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(time);
        _duration = duration;
        _time = time;
    }

    public bool IsOver => _time.GetElapsedTime(_start) >= _duration;

    public void Start() => _start = _time.GetTimestamp();
}
