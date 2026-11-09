using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class PriorityQueueTests
{
    private static PriorityCommandQueue Queue(int capacity = 10000) => new(capacity,
        new object(), new SemaphoreSlim(0), new PriorityQueue<Lib.ICommand, (long, long)>());

    [Fact]
    public void Highest_Priority_First_With_Fifo_Ties_And_Emergency_Override()
    {
        var queue = Queue(3);
        var seen = new List<int>();
        queue.Add(new PrioritizedCommand(new CallbackCommand(() => seen.Add(1)), -1));
        queue.Add(new PrioritizedCommand(new CallbackCommand(() => seen.Add(2)), 5));
        queue.Add(new PrioritizedCommand(new CallbackCommand(() => seen.Add(3)), 5));
        Assert.False(queue.TryAdd(new CallbackCommand(() => { })));
        queue.AddFirst(new CallbackCommand(() => seen.Add(0)));
        queue.CompleteAdding();
        foreach (var command in queue.Consume()) command.Execute();
        Assert.Equal(new[] { 0, 2, 3, 1 }, seen);
        Assert.False(queue.TryTake(out _));
        Assert.Throws<InvalidOperationException>(() => queue.Take());
        Assert.Throws<InvalidOperationException>(() => queue.Add(new CallbackCommand(() => { })));
    }

    [Fact]
    public async Task Concurrent_Producers_Preserve_Each_Producers_Fifo_And_Do_Not_Lose_Commands()
    {
        var queue = Queue();
        var seen = new List<(int Producer, int Index)>();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(producer => Task.Run(() =>
        {
            foreach (var index in Enumerable.Range(0, 100))
                queue.Add(new PrioritizedCommand(new CallbackCommand(() => seen.Add((producer, index))), 1));
        })));
        queue.CompleteAdding();
        foreach (var command in queue.Consume()) command.Execute();
        Assert.Equal(800, seen.Count);
        Assert.All(Enumerable.Range(0, 8), producer =>
            Assert.Equal(Enumerable.Range(0, 100), seen.Where(entry => entry.Producer == producer).Select(entry => entry.Index)));
    }

    [Fact]
    public async Task Registered_Priority_Thread_Drains_Low_Priority_Work_After_SoftStop()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
        var errors = new RecordingExceptionHandler();
        RuntimeFixture.Register(ServerThreadDependencies.ExceptionHandler, _ => errors);
        new RegisterIoCDependencyPriorityThreads(20).Execute();
        var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
        var thread = Ioc.Resolve<IServerThread>(ServerThreadDependencies.Create);
        var seen = new List<int>();
        foreach (var i in Enumerable.Range(0, 20)) thread.Add(new PrioritizedCommand(new CallbackCommand(() => seen.Add(i)), -10));
        thread.Add(Ioc.Resolve<Lib.ICommand>("Commands.Priority", Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, thread), 100));
        await thread.Start().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(Enumerable.Range(0, 20), seen);
        Assert.Empty(errors.Handled);
        using var entered = scope.Enter();
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>("Commands.Priority", "invalid"));
    }
}
