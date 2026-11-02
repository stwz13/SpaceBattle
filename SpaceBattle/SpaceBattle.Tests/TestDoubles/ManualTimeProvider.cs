namespace SpaceBattle.Tests;

public sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp = 5000;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => _timestamp;

    public void Advance(long milliseconds) => _timestamp += milliseconds;
}
