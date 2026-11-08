using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameTicksTests
{
    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(50);

    private sealed class Host : IDisposable
    {
        public TickTestClock Time { get; } = new();
        public Queue<ICommand> Server { get; } = new();
        public Queue<ICommand> Game { get; } = new();
        public List<Exception> Errors { get; } = [];
        public DelayedCommandScheduler Scheduler { get; }
        public Host() => Scheduler = new DelayedCommandScheduler(Time, Server.Enqueue, Errors.Add);
        public IGameTicks Create(Action step, int catchUp = 2) => new GameTickFactory(Scheduler)
            .Create(Period, new TickTestCommand(step), Game.Enqueue, catchUp);
        public void Deliver()
        {
            Time.FireDue();
            Server.Dequeue().Execute();
        }
        public void Dispose() => Scheduler.Dispose();
    }

    [Fact]
    public void Tick_Crosses_Both_Queues_And_Only_Then_Changes_The_World()
    {
        using var host = new Host();
        var position = 0;
        var ticks = host.Create(() => position += 3);
        new StartGameTicksCommand(ticks).Execute();
        Assert.True(ticks.IsRunning);
        Assert.Equal(Period, ticks.Step);
        Assert.Equal(0, ticks.TickNumber);
        Assert.Empty(host.Game);
        host.Time.Advance(Period);
        host.Time.FireDue();
        Assert.Equal(0, position);
        Assert.Empty(host.Game);

        host.Server.Dequeue().Execute();
        Assert.Equal(0, position);
        Assert.IsType<TickCommand>(Assert.Single(host.Game));
        host.Game.Dequeue().Execute();

        Assert.Equal(3, position);
        Assert.Equal(1, ticks.TickNumber);
        Assert.Empty(host.Server);
        Assert.Empty(host.Game);
        Assert.Equal(Period + Period, host.Time.NextDeadline);
    }

    [Fact]
    public void Input_Commands_And_Macro_Phases_Run_In_Game_Queue_Order()
    {
        using var host = new Host();
        var events = new List<string>();
        var moving = false;
        var step = new MacroCommand([
            new TickTestCommand(() => events.Add(moving ? "move" : "idle")),
            new TickTestCommand(() => events.Add("collisions"))]);
        var ticks = new GameTickFactory(host.Scheduler).Create(Period, step, host.Game.Enqueue);
        ticks.Start();
        host.Game.Enqueue(new TickTestCommand(() => moving = true));
        host.Time.Advance(Period);
        host.Deliver();
        host.Game.Enqueue(new TickTestCommand(() => moving = false));

        host.Game.Dequeue().Execute();
        host.Game.Dequeue().Execute();
        host.Game.Dequeue().Execute();
        Assert.Equal(new[] { "move", "collisions" }, events);
        host.Time.Advance(Period);
        host.Deliver();
        host.Game.Dequeue().Execute();
        Assert.Equal(new[] { "move", "collisions", "idle", "collisions" }, events);
    }

    [Fact]
    public void Starting_Twice_Does_Not_Create_Two_Chains_And_Duplicate_Ticks_Are_Ignored()
    {
        using var host = new Host();
        var steps = 0;
        var ticks = host.Create(() => steps++);
        ticks.Start();
        ticks.Start();
        host.Time.Advance(Period);
        host.Deliver();
        Assert.Empty(host.Server);
        var command = host.Game.Dequeue();

        command.Execute();
        command.Execute();

        Assert.Equal(1, steps);
        Assert.Equal(1, ticks.TickNumber);
        host.Time.Advance(Period);
        host.Time.FireDue();
        Assert.Single(host.Server);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Stop_Invalidates_Ticks_In_Timer_Server_Or_Game_Queue(int stage)
    {
        using var host = new Host();
        var steps = 0;
        var ticks = host.Create(() => steps++);
        ticks.Start();
        host.Time.Advance(Period);
        if (stage >= 1)
            host.Time.FireDue();
        if (stage >= 2)
            host.Server.Dequeue().Execute();

        new StopGameTicksCommand(ticks).Execute();
        ticks.Stop();
        host.Time.FireDue();
        foreach (var command in host.Server.ToArray())
            command.Execute();
        foreach (var command in host.Game.ToArray())
            command.Execute();

        Assert.False(ticks.IsRunning);
        Assert.Equal(0, steps);
        Assert.Equal(0, ticks.TickNumber);
        Assert.Null(host.Time.NextDeadline);
    }

    [Fact]
    public void Restart_Preserves_The_Step_Number_And_Rejects_The_Previous_Generation()
    {
        using var host = new Host();
        var steps = 0;
        var ticks = host.Create(() => steps++);
        ticks.Start();
        host.Time.Advance(Period);
        host.Deliver();
        host.Game.Dequeue().Execute();
        host.Time.Advance(Period);
        host.Deliver();
        var stale = host.Game.Dequeue();
        ticks.Stop();
        ticks.Start();

        stale.Execute();
        Assert.Equal(1, steps);
        host.Time.Advance(Period);
        host.Deliver();
        host.Game.Dequeue().Execute();

        Assert.Equal(2, steps);
        Assert.Equal(2, ticks.TickNumber);
    }

    [Fact]
    public void Next_Deadline_Uses_The_Previous_Deadline_Not_Completion_Time()
    {
        using var host = new Host();
        var ticks = host.Create(() => host.Time.Advance(TimeSpan.FromMilliseconds(7)));
        ticks.Start();
        host.Time.Advance(TimeSpan.FromMilliseconds(60));
        host.Deliver();
        host.Game.Dequeue().Execute();

        Assert.Equal(TimeSpan.FromMilliseconds(67), host.Scheduler.Now);
        Assert.Equal(TimeSpan.FromMilliseconds(100), host.Time.NextDeadline);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Overload_Allows_Only_Bounded_Catch_Up_And_Does_Not_Skip_Simulation_Steps(int limit)
    {
        using var host = new Host();
        var steps = 0;
        var ticks = host.Create(() => steps++, limit);
        ticks.Start();
        host.Time.Advance(TimeSpan.FromSeconds(1));

        for (var i = 0; i <= limit; i++)
        {
            host.Deliver();
            host.Game.Dequeue().Execute();
        }

        Assert.Equal(limit + 1, steps);
        Assert.Equal(limit + 1, ticks.TickNumber);
        Assert.Equal(TimeSpan.FromSeconds(1) + Period, host.Time.NextDeadline);
        host.Time.FireDue();
        Assert.Empty(host.Server);
    }

    [Fact]
    public void Two_Games_Share_The_Timer_But_Have_Independent_Queues_And_Lifecycles()
    {
        using var host = new Host();
        var secondQueue = new Queue<ICommand>();
        var firstSteps = 0;
        var secondSteps = 0;
        var first = host.Create(() => firstSteps++);
        var second = new GameTickFactory(host.Scheduler).Create(Period + Period,
            new TickTestCommand(() => secondSteps++), secondQueue.Enqueue);
        first.Start();
        second.Start();
        host.Time.Advance(Period);
        host.Deliver();
        host.Game.Dequeue().Execute();
        first.Stop();
        host.Time.Advance(Period);
        host.Deliver();

        Assert.Empty(host.Game);
        Assert.Equal(0, secondSteps);
        secondQueue.Dequeue().Execute();
        Assert.Equal(1, firstSteps);
        Assert.Equal(1, secondSteps);
        Assert.Equal(1, host.Time.TimersCreated);
        Assert.True(second.IsRunning);
    }

    [Fact]
    public void A_Failed_Step_Stops_The_Chain_And_Is_Not_Counted_As_Completed()
    {
        using var host = new Host();
        var failure = new InvalidOperationException("world step failed");
        var ticks = host.Create(() => throw failure);
        ticks.Start();
        host.Time.Advance(Period);
        host.Deliver();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => host.Game.Dequeue().Execute()));

        Assert.False(ticks.IsRunning);
        Assert.Same(failure, ticks.Failure);
        Assert.Equal(0, ticks.TickNumber);
        Assert.Null(host.Time.NextDeadline);
    }

    [Fact]
    public void A_Scheduling_Failure_Is_Recorded_And_After_Recovery_Start_Clears_It()
    {
        var scheduler = new Mock<IDelayedCommandScheduler>();
        var failure = new InvalidOperationException("scheduler stopped");
        scheduler.Setup(s => s.ScheduleAt(It.IsAny<TimeSpan>(), It.IsAny<ICommand>())).Throws(failure);
        var ticks = new GameTickFactory(scheduler.Object).Create(Period, new TickTestCommand(() => { }), _ => { });

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(ticks.Start));
        Assert.False(ticks.IsRunning);
        Assert.Same(failure, ticks.Failure);

        scheduler.Setup(s => s.ScheduleAt(It.IsAny<TimeSpan>(), It.IsAny<ICommand>()))
            .Returns(Mock.Of<IDisposable>());
        ticks.Start();
        Assert.True(ticks.IsRunning);
        Assert.Null(ticks.Failure);
    }

    [Fact]
    public void Failure_To_Schedule_The_Next_Tick_Stops_After_Counting_The_Completed_Step()
    {
        var scheduler = new Mock<IDelayedCommandScheduler>();
        ICommand? delivery = null;
        scheduler.Setup(s => s.ScheduleAt(It.IsAny<TimeSpan>(), It.IsAny<ICommand>()))
            .Callback<TimeSpan, ICommand>((_, command) => delivery = command)
            .Returns(Mock.Of<IDisposable>());
        var queue = new Queue<ICommand>();
        var ticks = new GameTickFactory(scheduler.Object).Create(Period, new TickTestCommand(() => { }), queue.Enqueue);
        ticks.Start();
        delivery!.Execute();
        var failure = new InvalidOperationException("cannot schedule");
        scheduler.Setup(s => s.ScheduleAt(It.IsAny<TimeSpan>(), It.IsAny<ICommand>())).Throws(failure);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => queue.Dequeue().Execute()));
        Assert.Equal(1, ticks.TickNumber);
        Assert.False(ticks.IsRunning);
        Assert.Same(failure, ticks.Failure);
    }

    [Fact]
    public void Stopping_During_A_World_Step_Does_Not_Schedule_A_Continuation()
    {
        using var host = new Host();
        IGameTicks? ticks = null;
        ticks = host.Create(() => ticks!.Stop());
        ticks.Start();
        host.Time.Advance(Period);
        host.Deliver();

        host.Game.Dequeue().Execute();

        Assert.False(ticks.IsRunning);
        Assert.Equal(1, ticks.TickNumber);
        Assert.Null(host.Time.NextDeadline);
    }

    [Fact]
    public void Reentrant_Execution_Of_The_Same_Tick_Is_Ignored()
    {
        using var host = new Host();
        ICommand? command = null;
        var steps = 0;
        var ticks = host.Create(() => { steps++; command!.Execute(); });
        ticks.Start();
        host.Time.Advance(Period);
        host.Deliver();
        command = host.Game.Dequeue();

        command.Execute();

        Assert.Equal(1, steps);
        Assert.Equal(1, ticks.TickNumber);
    }

    [Fact]
    public void Invalid_Options_And_Dependencies_Are_Rejected()
    {
        using var host = new Host();
        var factory = new GameTickFactory(host.Scheduler);
        var step = new TickTestCommand(() => { });
        Assert.Throws<ArgumentNullException>(() => new GameTickFactory(null!));
        Assert.Throws<ArgumentNullException>(() => factory.Create(Period, null!, host.Game.Enqueue));
        Assert.Throws<ArgumentNullException>(() => factory.Create(Period, step, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(TimeSpan.Zero, step, host.Game.Enqueue));
        Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(-Period, step, host.Game.Enqueue));
        Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(Period, step, host.Game.Enqueue, -1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Delivery_Failure_Stops_The_Chain_And_Allows_A_Fresh_Start(bool failAtServer)
    {
        var time = new TickTestClock();
        var server = new Queue<ICommand>();
        var game = new Queue<ICommand>();
        var errors = new List<Exception>();
        var failure = new InvalidOperationException("Queue rejected the tick");
        var reject = true;
        using var scheduler = new DelayedCommandScheduler(time, command =>
        {
            if (reject && failAtServer)
                throw failure;
            server.Enqueue(command);
        }, errors.Add);
        var ticks = new GameTickFactory(scheduler).Create(Period, new TickTestCommand(() => { }), command =>
        {
            if (reject && !failAtServer)
                throw failure;
            game.Enqueue(command);
        });

        ticks.Start();
        time.Advance(Period);
        time.FireDue();
        if (failAtServer)
            Assert.Same(failure, Assert.Single(errors));
        else
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(server.Dequeue().Execute));

        Assert.False(ticks.IsRunning);
        Assert.Same(failure, ticks.Failure);
        Assert.Null(time.NextDeadline);
        Assert.Equal(0, ticks.TickNumber);

        reject = false;
        ticks.Start();
        time.Advance(Period);
        time.FireDue();
        server.Dequeue().Execute();
        game.Dequeue().Execute();

        Assert.True(ticks.IsRunning);
        Assert.Null(ticks.Failure);
        Assert.Equal(1, ticks.TickNumber);
    }

    [Fact]
    public void Delivery_Failure_From_A_Previous_Generation_Does_Not_Stop_A_Restart()
    {
        using var host = new Host();
        var ticks = new GameTickFactory(host.Scheduler).Create(Period, new TickTestCommand(() => { }),
            _ => throw new InvalidOperationException("Queue rejected the tick"));
        ticks.Start();
        host.Time.Advance(Period);
        host.Time.FireDue();
        var staleDelivery = host.Server.Dequeue();

        ticks.Stop();
        ticks.Start();
        Assert.Throws<InvalidOperationException>(staleDelivery.Execute);

        Assert.True(ticks.IsRunning);
        Assert.Null(ticks.Failure);
        Assert.Equal(Period + Period, host.Time.NextDeadline);
    }
}
