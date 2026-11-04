using System.Collections.Concurrent;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ErrorPolicyTests
{
    [Theory]
    [InlineData(ErrorAction.Retry, 2, 3)]
    [InlineData(ErrorAction.Skip, 0, 1)]
    public async Task Retry_Is_Bounded_And_Skip_Continues_With_The_Next_Message(ErrorAction action, int retries, int expected)
    {
        await using var fixture = new RuntimeFixture(1);
        var calls = 0;
        using (fixture.Scope.Enter())
        {
            RuntimeFixture.Register("Messages.Command.Fail", _ => new CallbackCommand(() => { calls++; throw new IOException("failed"); }));
            new RegisterIoCDependencyErrorPolicy(new Dictionary<Type, (ErrorAction, int)> { [typeof(IOException)] = (action, retries) }).Execute();
        }
        var host = fixture.Runtime.Start("a");
        host.Send(new Message("a", "Fail", default));
        host.Send(RuntimeFixture.Message("a", 7));
        await host.Enqueue(() => { });
        Assert.Equal(expected, calls);
        Assert.Equal(7, Assert.Single(fixture.Seen).Value);
        Assert.Equal(expected, fixture.Resolve<MemoryLog>("Errors.Log").Entries.Count);
        Assert.Empty(fixture.Errors.Handled);
    }

    [Fact]
    public async Task Stopping_One_Game_Does_Not_Stop_Another_Game_On_The_Same_Thread()
    {
        await using var fixture = new RuntimeFixture(1);
        using (fixture.Scope.Enter())
        {
            RuntimeFixture.Register("Messages.Command.Fail", _ => new CallbackCommand(() => throw new IOException("failed")));
            new RegisterIoCDependencyErrorPolicy(new Dictionary<Type, (ErrorAction, int)> { [typeof(IOException)] = (ErrorAction.StopGame, 0) }).Execute();
        }
        var first = fixture.Runtime.Start("a");
        var second = fixture.Runtime.Start("b");
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = first.Execute(() => { entered.SetResult(); release.Wait(TimeSpan.FromSeconds(10)); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            first.Send(new Message("a", "Fail", default));
            first.Send(RuntimeFixture.Message("a", 1));
            second.Send(RuntimeFixture.Message("b", 2));
        }
        finally { release.Set(); }
        await blocked;
        await second.Enqueue(() => { });
        Assert.Equal(GameHostState.Stopped, first.State);
        Assert.Equal(("b", 2), Assert.Single(fixture.Seen));
        Assert.Empty(fixture.Errors.Handled);
    }

    [Fact]
    public async Task StopThread_Removes_It_From_Routing_And_Ends_Its_Task()
    {
        await using var fixture = new RuntimeFixture(1);
        using (fixture.Scope.Enter())
        {
            RuntimeFixture.Register("Messages.Command.Fail", _ => new CallbackCommand(() => throw new IOException("failed")));
            new RegisterIoCDependencyErrorPolicy(new Dictionary<Type, (ErrorAction, int)> { [typeof(IOException)] = (ErrorAction.StopThread, 0) }).Execute();
        }
        var thread = ((Server)fixture.Runtime.Server).Threads.Single();
        fixture.Runtime.Start("a").Send(new Message("a", "Fail", default));
        await thread.Completion!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, fixture.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring).Count);
        // HardStop already completed this thread; Server.Stop would otherwise enqueue to a closed inbox.
        Assert.Equal(GameHostState.Stopped, ((ManagedGameHost)fixture.Runtime.Find("a")).State);
    }
}
