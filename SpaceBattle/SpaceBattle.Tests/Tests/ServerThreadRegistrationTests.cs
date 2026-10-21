using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ServerThreadRegistrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly RecordingExceptionHandler _handler = new();

    public ServerThreadRegistrationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", ServerThreadDependencies.ExceptionHandler,
            (object[] args) => _handler).Execute();
        new RegisterIoCDependencyServerThread().Execute();
    }

    private static IServerThread Create() => Ioc.Resolve<IServerThread>(ServerThreadDependencies.Create);

    private static Lib.ICommand Stop(string key, IServerThread thread) => Ioc.Resolve<Lib.ICommand>(key, thread);

    [Fact]
    public void Created_Thread_Is_New_Every_Time_And_Is_Not_Started()
    {
        var first = Create();
        var second = Create();

        Assert.NotSame(first, second);
        Assert.Equal(0, first.ThreadId);
        Assert.Equal(0, second.ThreadId);
    }

    [Fact]
    public void Created_Thread_Runs_Commands_And_Stops_With_The_Registered_Soft_Stop()
    {
        var thread = Create();
        var executed = 0;
        thread.Add(new DelegateStep(() => executed++));
        thread.Add(Stop(ServerThreadDependencies.SoftStop, thread));
        thread.Add(new DelegateStep(() => executed++));

        Assert.True(thread.Start().Wait(Timeout));

        Assert.Equal(2, executed);
    }

    [Fact]
    public void Created_Thread_Stops_Immediately_With_The_Registered_Hard_Stop()
    {
        var thread = Create();
        var executed = 0;
        thread.Add(Stop(ServerThreadDependencies.HardStop, thread));
        thread.Add(new DelegateStep(() => executed++));

        Assert.True(thread.Start().Wait(Timeout));

        Assert.Equal(0, executed);
    }

    [Fact]
    public void Created_Thread_Reports_Failures_To_The_Registered_Handler()
    {
        var thread = Create();
        var failure = new InvalidOperationException("boom");
        var bad = new DelegateStep(() => throw failure);
        thread.Add(bad);
        thread.Add(Stop(ServerThreadDependencies.SoftStop, thread));

        Assert.True(thread.Start().Wait(Timeout));

        var handled = Assert.Single(_handler.Handled.ToArray());
        Assert.Same(bad, handled.Command);
        Assert.Same(failure, handled.Exception);
    }

    [Fact]
    public void Registered_Stop_Commands_Refuse_To_Run_Outside_The_Thread()
    {
        var thread = Create();

        Assert.IsType<HardStopCommand>(Stop(ServerThreadDependencies.HardStop, thread));
        Assert.IsType<SoftStopCommand>(Stop(ServerThreadDependencies.SoftStop, thread));
        Assert.Throws<InvalidOperationException>(Stop(ServerThreadDependencies.HardStop, thread).Execute);
        Assert.Throws<InvalidOperationException>(Stop(ServerThreadDependencies.SoftStop, thread).Execute);
    }

    [Theory]
    [InlineData("ServerThreads.Create")]
    public void Thread_Takes_No_Arguments(string key)
    {
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IServerThread>(key, 1));
    }

    [Theory]
    [InlineData("Commands.HardStop")]
    [InlineData("Commands.SoftStop")]
    public void Stop_Commands_Need_Exactly_One_Server_Thread(string key)
    {
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(key));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(key, "thread"));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(key, Create(), Create()));
    }

    [Fact]
    public void Thread_Registered_With_A_Capacity_Refuses_Messages_When_Its_Inbox_Is_Full()
    {
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyServerThread(1).Execute();
        var thread = Create();

        Assert.True(thread.TryAdd(new DelegateStep(() => { })));
        Assert.False(thread.TryAdd(new DelegateStep(() => { })));
    }

    [Fact]
    public void Registration_Rejects_A_Capacity_Below_One()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyServerThread(0));
    }
}
