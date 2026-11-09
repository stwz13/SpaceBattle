using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class DelayedCommandSchedulerTests
{
    [Fact]
    public void Delivers_Only_When_Due_And_Never_Executes_The_Command()
    {
        var time = new TickTestClock();
        var inbox = new Queue<ICommand>();
        var executed = false;
        using var scheduler = new DelayedCommandScheduler(time, inbox.Enqueue, error => throw error);
        var command = new TickTestCommand(() => executed = true);
        scheduler.ScheduleAt(TimeSpan.FromMilliseconds(50), command);

        time.Advance(TimeSpan.FromMilliseconds(49));
        time.FireDue();
        Assert.Empty(inbox);
        time.Advance(TimeSpan.FromMilliseconds(1));
        time.FireDue();

        Assert.Same(command, Assert.Single(inbox));
        Assert.False(executed);
        inbox.Dequeue().Execute();
        Assert.True(executed);
        time.FireDue();
        Assert.Empty(inbox);
    }

    [Fact]
    public void Shares_One_Timer_And_Orders_Deadlines_With_Stable_Ties()
    {
        var time = new TickTestClock();
        var inbox = new List<ICommand>();
        using var scheduler = new DelayedCommandScheduler(time, inbox.Add, error => throw error);
        var later = new TickTestCommand(() => { });
        var first = new TickTestCommand(() => { });
        var second = new TickTestCommand(() => { });
        scheduler.ScheduleAt(TimeSpan.FromMilliseconds(100), later);
        scheduler.ScheduleAt(TimeSpan.FromMilliseconds(50), first);
        scheduler.ScheduleAt(TimeSpan.FromMilliseconds(50), second);

        time.Advance(TimeSpan.FromMilliseconds(100));
        time.FireDue();

        Assert.Equal(new ICommand[] { first, second, later }, inbox);
        Assert.Equal(1, time.TimersCreated);
        Assert.True(time.TimerCreatedWithFlowSuppressed);
        Assert.Null(time.NextDeadline);
    }

    [Fact]
    public void Cancellation_Rearms_The_Timer_And_Removes_The_Delivery()
    {
        var time = new TickTestClock();
        var inbox = new List<ICommand>();
        using var scheduler = new DelayedCommandScheduler(time, inbox.Add, error => throw error);
        var first = scheduler.ScheduleAt(TimeSpan.FromMilliseconds(10), new TickTestCommand(() => { }));
        var last = scheduler.ScheduleAt(TimeSpan.FromMilliseconds(100), new TickTestCommand(() => { }));

        first.Dispose();
        first.Dispose();
        Assert.Equal(TimeSpan.FromMilliseconds(100), time.NextDeadline);
        last.Dispose();
        Assert.Null(time.NextDeadline);
        time.Advance(TimeSpan.FromSeconds(1));
        time.FireDue();
        Assert.Empty(inbox);
    }

    [Fact]
    public void A_Delivery_Can_Be_Cancelled_After_The_Timer_Selected_It()
    {
        var time = new TickTestClock();
        var inbox = new List<ICommand>();
        IDisposable? second = null;
        using var scheduler = new DelayedCommandScheduler(time, command =>
        {
            inbox.Add(command);
            second!.Dispose();
        }, error => throw error);
        scheduler.ScheduleAt(TimeSpan.Zero, new TickTestCommand(() => { }));
        second = scheduler.ScheduleAt(TimeSpan.Zero, new TickTestCommand(() => { }));

        time.FireDue();

        Assert.Single(inbox);
    }

    [Fact]
    public void Reports_Failed_Dispatch_And_Continues_Other_Games()
    {
        var time = new TickTestClock();
        var errors = new List<Exception>();
        var inbox = new List<ICommand>();
        var rejected = new TickTestCommand(() => { });
        var accepted = new TickTestCommand(() => { });
        var failure = new InvalidOperationException("server stopped");
        using var scheduler = new DelayedCommandScheduler(time, command =>
        {
            if (command == rejected)
                throw failure;
            inbox.Add(command);
        }, errors.Add);
        scheduler.ScheduleAt(TimeSpan.Zero, rejected);
        scheduler.ScheduleAt(TimeSpan.Zero, accepted);

        time.FireDue();

        Assert.Same(failure, Assert.Single(errors));
        Assert.Same(accepted, Assert.Single(inbox));
    }

    [Fact]
    public void Past_Deadlines_Are_Deferred_Until_The_Timer_Callback()
    {
        var time = new TickTestClock();
        var inbox = new List<ICommand>();
        using var scheduler = new DelayedCommandScheduler(time, inbox.Add, error => throw error);
        time.Advance(TimeSpan.FromSeconds(1));
        scheduler.ScheduleAt(TimeSpan.Zero, new TickTestCommand(() => { }));
        Assert.Empty(inbox);

        time.FireDue();

        Assert.Single(inbox);
    }

    [Fact]
    public void Fractional_Milliseconds_Do_Not_Produce_Early_Timer_Spinning()
    {
        var time = new TickTestClock();
        var inbox = new List<ICommand>();
        using var scheduler = new DelayedCommandScheduler(time, inbox.Add, error => throw error);
        scheduler.ScheduleAt(TimeSpan.FromTicks(1), new TickTestCommand(() => { }));
        Assert.Equal(TimeSpan.FromMilliseconds(1), time.NextDeadline);
        time.Advance(TimeSpan.FromTicks(1));
        time.FireDue();
        Assert.Empty(inbox);
        time.Advance(TimeSpan.FromMilliseconds(1));
        time.FireDue();
        Assert.Single(inbox);
    }

    [Fact]
    public void Long_Deadlines_Are_Armed_In_Timer_Sized_Chunks()
    {
        var time = new TickTestClock();
        var inbox = new List<ICommand>();
        using var scheduler = new DelayedCommandScheduler(time, inbox.Add, error => throw error);
        scheduler.ScheduleAt(TimeSpan.FromDays(100), new TickTestCommand(() => { }));
        var maximum = TimeSpan.FromMilliseconds(0xfffffffe);
        Assert.Equal(maximum, time.NextDeadline);

        time.Advance(maximum);
        time.FireDue();
        Assert.Empty(inbox);
        Assert.Equal(maximum + maximum, time.NextDeadline);
    }

    [Fact]
    public void Dispose_Drops_Pending_Deliveries_And_Rejects_New_Ones()
    {
        var time = new TickTestClock();
        var inbox = new List<ICommand>();
        var scheduler = new DelayedCommandScheduler(time, inbox.Add, error => throw error);
        var pending = scheduler.ScheduleAt(TimeSpan.Zero, new TickTestCommand(() => { }));
        scheduler.Dispose();
        scheduler.Dispose();
        pending.Dispose();
        time.FireDue();

        Assert.Empty(inbox);
        Assert.Null(time.NextDeadline);
        Assert.Throws<ObjectDisposedException>(() => scheduler.ScheduleAt(TimeSpan.Zero, new TickTestCommand(() => { })));
    }

    [Fact]
    public void Validates_Required_Dependencies()
    {
        var time = new TickTestClock();
        Assert.Throws<ArgumentNullException>(() => new DelayedCommandScheduler(null!, _ => { }, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new DelayedCommandScheduler(time, null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new DelayedCommandScheduler(time, _ => { }, null!));
        using var scheduler = new DelayedCommandScheduler(time, _ => { }, _ => { });
        Assert.Throws<ArgumentNullException>(() => scheduler.ScheduleAt(TimeSpan.Zero, null!));
    }
}
