using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CommandCollectionsTests
{
    private static DelegateStep Step() => new(() => { });

    [Fact]
    public void Queue_Gives_Commands_In_The_Order_They_Were_Added()
    {
        var queue = new BlockingCommandQueue();
        var first = Step();
        var second = Step();

        queue.Add(first);
        queue.Add(second);

        Assert.Equal(2, queue.Count);
        Assert.Same(first, queue.Take());
        Assert.True(queue.TryTake(out var taken));
        Assert.Same(second, taken);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Empty_Queue_Does_Not_Block_On_TryTake()
    {
        var queue = new BlockingCommandQueue();

        Assert.False(queue.TryTake(out var taken));
        Assert.Null(taken);
    }

    [Fact]
    public void Queue_Rejects_Nothing()
    {
        Assert.Throws<ArgumentNullException>(() => new BlockingCommandQueue().Add(null!));
    }

    [Fact]
    public void Completed_Queue_Rejects_New_Commands_But_Gives_The_Remaining_Ones_Then_Ends()
    {
        var queue = new BlockingCommandQueue();
        var first = Step();
        var second = Step();
        queue.Add(first);
        queue.Add(second);

        queue.CompleteAdding();

        Assert.Throws<InvalidOperationException>(() => queue.Add(Step()));
        Assert.Equal([first, second], queue.Consume().ToArray());
    }

    [Fact]
    public void Consume_Waits_For_Commands_Added_From_Another_Thread()
    {
        var queue = new BlockingCommandQueue();
        var command = Step();
        var consumed = Task.Run(() => queue.Consume().ToArray());

        queue.Add(command);
        queue.CompleteAdding();

        Assert.True(consumed.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal([command], consumed.Result);
    }

    [Fact]
    public void Scheduler_Gives_Commands_In_The_Order_They_Were_Added()
    {
        var scheduler = new QueueScheduler();
        var first = Step();
        var second = Step();
        Assert.False(scheduler.HasCommand());

        scheduler.Add(first);
        scheduler.Add(second);

        Assert.True(scheduler.HasCommand());
        Assert.Same(first, scheduler.Select());
        Assert.Same(second, scheduler.Select());
        Assert.False(scheduler.HasCommand());
    }

    [Fact]
    public void Scheduler_Cannot_Select_From_Nothing_And_Rejects_Nothing()
    {
        var scheduler = new QueueScheduler();

        Assert.Throws<InvalidOperationException>(() => scheduler.Select());
        Assert.Throws<ArgumentNullException>(() => scheduler.Add(null!));
    }

    [Fact]
    public void Command_Added_First_Is_Taken_Before_The_Ones_Already_Waiting()
    {
        var queue = new BlockingCommandQueue();
        var first = Step();
        var second = Step();
        var urgent = Step();
        queue.Add(first);
        queue.Add(second);

        queue.AddFirst(urgent);

        Assert.Equal(3, queue.Count);
        Assert.Equal([urgent, first, second], new[] { queue.Take(), queue.Take(), queue.Take() });
    }

    [Fact]
    public void Several_Urgent_Commands_Come_Out_Latest_First()
    {
        var queue = new BlockingCommandQueue();
        var normal = Step();
        var older = Step();
        var newer = Step();
        queue.Add(normal);

        queue.AddFirst(older);
        queue.AddFirst(newer);

        Assert.Equal([newer, older, normal], new[] { queue.Take(), queue.Take(), queue.Take() });
    }

    [Fact]
    public void Urgent_Command_Wakes_A_Consumer_Waiting_On_An_Empty_Queue()
    {
        var queue = new BlockingCommandQueue();
        var urgent = Step();
        var taken = Task.Run(() => queue.Take());
        Assert.False(taken.Wait(TimeSpan.FromMilliseconds(200)));

        queue.AddFirst(urgent);

        Assert.True(taken.Wait(TimeSpan.FromSeconds(10)));
        Assert.Same(urgent, taken.Result);
    }

    [Fact]
    public void Completed_Queue_Rejects_Urgent_Commands_Too()
    {
        var queue = new BlockingCommandQueue();
        queue.CompleteAdding();

        Assert.Throws<InvalidOperationException>(() => queue.AddFirst(Step()));
        Assert.Throws<ArgumentNullException>(() => new BlockingCommandQueue().AddFirst(null!));
    }

    [Fact]
    public void Completed_Empty_Queue_Throws_On_Take_And_Gives_Nothing_On_TryTake_Every_Time()
    {
        var queue = new BlockingCommandQueue();
        queue.CompleteAdding();
        queue.CompleteAdding();

        Assert.Throws<InvalidOperationException>(() => queue.Take());
        Assert.Throws<InvalidOperationException>(() => queue.Take());
        Assert.False(queue.TryTake(out var taken));
        Assert.Null(taken);
        Assert.False(queue.TryTake(out _));
        Assert.Empty(queue.Consume().ToArray());
    }

    [Fact]
    public void Completing_The_Queue_Releases_Every_Waiting_Consumer()
    {
        var queue = new BlockingCommandQueue();
        var consumers = Enumerable.Range(0, 5).Select(_ => Task.Run(() => queue.Consume().ToArray())).ToArray();
        Assert.False(Task.WaitAll(consumers, TimeSpan.FromMilliseconds(200)));

        queue.CompleteAdding();

        Assert.True(Task.WaitAll(consumers, TimeSpan.FromSeconds(10)));
        Assert.All(consumers, consumer => Assert.Empty(consumer.Result));
    }

    [Fact]
    public void Remaining_Commands_Are_Given_Out_After_Completion_Before_The_Queue_Ends()
    {
        var queue = new BlockingCommandQueue();
        var first = Step();
        var urgent = Step();
        queue.Add(first);
        queue.AddFirst(urgent);
        queue.CompleteAdding();

        Assert.Equal([urgent, first], queue.Consume().ToArray());
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Nothing_Is_Lost_Or_Duplicated_With_Many_Producers_And_Consumers()
    {
        var queue = new BlockingCommandQueue();
        var seen = new System.Collections.Concurrent.ConcurrentBag<ICommand>();
        var consumers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            foreach (var command in queue.Consume())
            {
                seen.Add(command);
            }
        })).ToArray();
        var sent = Enumerable.Range(0, 2000).Select(_ => Step()).ToArray();

        Parallel.ForEach(sent.Select((command, index) => (command, index)),
            pair => (pair.index % 3 == 0 ? (Action<ICommand>)queue.AddFirst : queue.Add)(pair.command));
        queue.CompleteAdding();

        Assert.True(Task.WaitAll(consumers, TimeSpan.FromSeconds(30)));
        Assert.Equal(2000, seen.Count);
        Assert.Equal(2000, seen.Distinct().Count());
        Assert.All(sent, command => Assert.Contains(command, seen));
    }

    [Fact]
    public void Producer_And_Consumer_Views_Work_On_The_Same_Queue()
    {
        var queue = new BlockingCommandQueue();
        ICommandProducer producer = queue;
        ICommandConsumer consumer = queue;
        var command = Step();

        producer.Add(command);

        Assert.Equal(1, consumer.Count);
        Assert.Same(command, consumer.Take());
    }

    [Fact]
    public void TryAdd_Refuses_A_Command_While_The_Queue_Is_Full()
    {
        var queue = new BlockingCommandQueue(2);
        var first = Step();
        var second = Step();
        var third = Step();

        Assert.True(queue.TryAdd(first));
        Assert.True(queue.TryAdd(second));
        Assert.False(queue.TryAdd(third));
        Assert.Equal(2, queue.Count);
        Assert.Same(first, queue.Take());
        Assert.True(queue.TryAdd(third));
        Assert.Equal([second, third], queue.Consume().Take(2).ToArray());
    }

    [Fact]
    public void Add_And_AddFirst_Ignore_The_Capacity()
    {
        var queue = new BlockingCommandQueue(1);
        var urgent = Step();

        queue.Add(Step());
        queue.Add(Step());
        queue.AddFirst(urgent);

        Assert.Equal(3, queue.Count);
        Assert.False(queue.TryAdd(Step()));
        Assert.Same(urgent, queue.Take());
    }

    [Fact]
    public void Capacity_Must_Be_Positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockingCommandQueue(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BlockingCommandQueue(-1));
    }

    [Fact]
    public void TryAdd_Rejects_Nothing_And_A_Completed_Queue()
    {
        var queue = new BlockingCommandQueue(1);

        Assert.Throws<ArgumentNullException>(() => queue.TryAdd(null!));
        queue.CompleteAdding();
        Assert.Throws<InvalidOperationException>(() => queue.TryAdd(Step()));
    }

    [Fact]
    public void Queue_Without_A_Capacity_Always_Accepts_TryAdd()
    {
        var queue = new BlockingCommandQueue();

        Assert.All(Enumerable.Range(0, 1000), _ => Assert.True(queue.TryAdd(Step())));
        Assert.Equal(1000, queue.Count);
    }
}
