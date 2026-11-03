using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameTickRegistrationTests
{
    public GameTickRegistrationTests()
    {
        new InitCommand().Execute();
        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
    }

    private static void SetScope(object scope) =>
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();

    [Fact]
    public void Registered_Commands_Start_And_Stop_A_Game_Through_The_Existing_Queues()
    {
        var clock = new TickTestClock();
        var server = new Queue<Lib.ICommand>();
        var game = new Queue<Lib.ICommand>();
        var steps = 0;
        using var scheduler = new DelayedCommandScheduler(clock, server.Enqueue, error => throw error);
        new RegisterIoCDependencyGameTicks(scheduler).Execute();
        var factory = Ioc.Resolve<IGameTickFactory>(GameTickDependencies.Factory);
        Assert.Same(factory, Ioc.Resolve<IGameTickFactory>(GameTickDependencies.Factory));
        var ticks = Ioc.Resolve<IGameTicks>(GameTickDependencies.Create, TimeSpan.FromMilliseconds(50),
            new TickTestCommand(() => steps++), (Action<Lib.ICommand>)game.Enqueue, 0);

        Ioc.Resolve<Lib.ICommand>(GameTickDependencies.Start, ticks).Execute();
        clock.Advance(TimeSpan.FromMilliseconds(50));
        clock.FireDue();
        server.Dequeue().Execute();
        game.Dequeue().Execute();
        Ioc.Resolve<Lib.ICommand>(GameTickDependencies.Stop, ticks).Execute();

        Assert.Equal(1, steps);
        Assert.False(ticks.IsRunning);
        Assert.Null(clock.NextDeadline);
    }

    [Fact]
    public void Child_Scopes_Use_Their_Own_Schedulers_And_Preserve_The_Parent_Factory()
    {
        var parentScheduler = new Mock<IDelayedCommandScheduler>();
        new RegisterIoCDependencyGameTicks(parentScheduler.Object).Execute();
        var parentFactory = Ioc.Resolve<IGameTickFactory>(GameTickDependencies.Factory);
        var parent = Ioc.Resolve<object>("IoC.Scope.Current");
        var childScheduler = new Mock<IDelayedCommandScheduler>();
        childScheduler.Setup(s => s.ScheduleAt(It.IsAny<TimeSpan>(), It.IsAny<Lib.ICommand>()))
            .Returns(Mock.Of<IDisposable>());

        SetScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        try
        {
            new RegisterIoCDependencyGameTicks(childScheduler.Object).Execute();
            var factory = Ioc.Resolve<IGameTickFactory>(GameTickDependencies.Factory);
            Assert.NotSame(parentFactory, factory);
            var first = factory.Create(TimeSpan.FromMilliseconds(10), new TickTestCommand(() => { }), _ => { });
            var second = factory.Create(TimeSpan.FromMilliseconds(20), new TickTestCommand(() => { }), _ => { });
            first.Start();
            Assert.True(first.IsRunning);
            Assert.False(second.IsRunning);
            childScheduler.Verify(s => s.ScheduleAt(TimeSpan.FromMilliseconds(10), It.IsAny<Lib.ICommand>()), Times.Once);
            parentScheduler.VerifyNoOtherCalls();
        }
        finally
        {
            SetScope(parent);
        }

        Assert.Same(parentFactory, Ioc.Resolve<IGameTickFactory>(GameTickDependencies.Factory));
    }

    [Fact]
    public void Registrations_Reject_Invalid_Arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyGameTicks(null!));
        new RegisterIoCDependencyGameTicks(Mock.Of<IDelayedCommandScheduler>()).Execute();
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameTickFactory>(GameTickDependencies.Factory, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameTicks>(GameTickDependencies.Create));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameTicks>(GameTickDependencies.Create,
            TimeSpan.FromMilliseconds(50), new TickTestCommand(() => { }), (Action<Lib.ICommand>)(_ => { }), "invalid"));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(GameTickDependencies.Start));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(GameTickDependencies.Stop, "invalid"));
    }
}
