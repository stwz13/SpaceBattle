using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class TimeQuantumTests
{
    [Fact]
    public void Time_Quantum_Is_Over_Exactly_When_Its_Duration_Has_Passed()
    {
        var time = new ManualTimeProvider();
        var quantum = new TimeQuantum(TimeSpan.FromMilliseconds(100), time);

        quantum.Start();
        Assert.False(quantum.IsOver);
        time.Advance(99);
        Assert.False(quantum.IsOver);
        time.Advance(1);
        Assert.True(quantum.IsOver);
    }

    [Fact]
    public void Time_Quantum_Starts_Counting_Again_On_Every_Start()
    {
        var time = new ManualTimeProvider();
        var quantum = new TimeQuantum(TimeSpan.FromMilliseconds(100), time);
        quantum.Start();
        time.Advance(150);
        Assert.True(quantum.IsOver);

        quantum.Start();

        Assert.False(quantum.IsOver);
        time.Advance(100);
        Assert.True(quantum.IsOver);
    }

    [Fact]
    public void Zero_Duration_Time_Quantum_Is_Over_Right_After_Start()
    {
        var quantum = new TimeQuantum(TimeSpan.Zero, new ManualTimeProvider());

        quantum.Start();

        Assert.True(quantum.IsOver);
    }

    [Fact]
    public void Time_Quantum_Rejects_Invalid_Arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeQuantum(TimeSpan.FromSeconds(-1), new ManualTimeProvider()));
        Assert.Throws<ArgumentNullException>(() => new TimeQuantum(TimeSpan.Zero, null!));
    }
}
