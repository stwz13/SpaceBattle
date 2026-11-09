using System.Collections.Concurrent;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ExceptionFactory _exceptions = new();
    private readonly RecordingExceptionHandler _handler = new();
    private readonly ConsistentHashRing<IServerThread> _ring = new(new Sha256HashFunction(), 32);
    private readonly List<ServerThread> _created = [];

    private ServerThread NewThread()
    {
        var thread = new ServerThread(new BlockingCommandQueue(), new BlockingCommandQueue(), new QueueScheduler(),
            _handler, _exceptions);
        _created.Add(thread);
        return thread;
    }

    private Server NewServer(int threads, string prefix = "Thread") => new(_ring, NewThread,
        thread => new SoftStopCommand(thread, _exceptions), thread => new HardStopCommand(thread, _exceptions),
        threads, prefix);

    [Fact]
    public void New_Server_Has_No_Threads_And_Is_Not_Running()
    {
        var server = NewServer(5);

        Assert.False(server.IsRunning);
        Assert.Equal(0, server.ThreadCount);
        Assert.Equal(0, _ring.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(30)]
    public async Task Start_Creates_The_Configured_Number_Of_Threads_And_Puts_Them_On_The_Ring(int count)
    {
        var server = NewServer(count);

        server.Start();

        Assert.True(server.IsRunning);
        Assert.Equal(count, server.ThreadCount);
        Assert.Equal(count, _ring.Count);
        Assert.Equal(count, _created.Count);
        await server.SoftStop().WaitAsync(Timeout);
    }

    [Fact]
    public async Task Every_Thread_Is_Started_And_Runs_Commands_On_Its_Own_Thread()
    {
        var server = NewServer(3);
        var ids = new ConcurrentBag<int>();
        server.Start();

        foreach (var thread in _created)
        {
            thread.Add(new DelegateStep(() => ids.Add(Environment.CurrentManagedThreadId)));
        }
        Assert.True(SpinWait.SpinUntil(() => ids.Count == 3, Timeout));

        Assert.Equal(3, ids.Distinct().Count());
        Assert.Equal(_created.Select(thread => thread.ThreadId).Order(), ids.Order());
        await server.SoftStop().WaitAsync(Timeout);
    }

    [Fact]
    public async Task Server_Cannot_Be_Started_Twice()
    {
        var server = NewServer(1);
        server.Start();

        Assert.Throws<InvalidOperationException>(() => server.Start());
        await server.SoftStop().WaitAsync(Timeout);
    }

    [Fact]
    public async Task Thread_Can_Be_Added_To_A_Running_Server_And_Starts_Right_Away()
    {
        var server = NewServer(2);
        server.Start();
        var ran = new ManualResetEventSlim();

        var added = server.AddThread();
        added.Add(new DelegateStep(ran.Set));

        Assert.True(ran.Wait(Timeout));
        Assert.Equal(3, server.ThreadCount);
        Assert.Equal(3, _ring.Count);
        await server.SoftStop().WaitAsync(Timeout);
    }

    [Fact]
    public async Task Thread_Added_Before_Start_Waits_For_Start()
    {
        var server = NewServer(2);

        var added = server.AddThread();

        Assert.Equal(0, added.ThreadId);
        server.Start();
        Assert.Equal(3, server.ThreadCount);
        Assert.True(SpinWait.SpinUntil(() => added.ThreadId != 0, Timeout));
        await server.SoftStop().WaitAsync(Timeout);
    }

    [Fact]
    public async Task Adding_A_Thread_Only_Moves_A_Part_Of_The_Games()
    {
        var server = NewServer(5);
        server.Start();
        var keys = Enumerable.Range(0, 1000).Select(index => $"Battle-{index}").ToArray();
        var before = keys.ToDictionary(key => key, key => _ring.Find(key));

        var added = server.AddThread();

        var moved = keys.Where(key => !ReferenceEquals(before[key], _ring.Find(key))).ToArray();
        Assert.All(moved, key => Assert.Same(added, _ring.Find(key)));
        Assert.InRange(moved.Length, 1, 600);
        await server.SoftStop().WaitAsync(Timeout);
    }

    [Fact]
    public async Task Soft_Stop_Finishes_Everything_Already_Queued_And_Ends_All_Threads()
    {
        var server = NewServer(3);
        var done = new ConcurrentBag<int>();
        server.Start();
        foreach (var thread in _created)
        {
            foreach (var number in Enumerable.Range(0, 100))
            {
                thread.Add(new DelegateStep(() => done.Add(number)));
            }
        }

        var stopped = server.SoftStop();

        await stopped.WaitAsync(Timeout);
        Assert.Equal(300, done.Count);
        Assert.False(server.IsRunning);
        Assert.Equal(0, _ring.Count);
        Assert.All(_created, thread => Assert.Throws<InvalidOperationException>(() => thread.Add(new DelegateStep(() => { }))));
    }

    [Fact]
    public async Task Soft_Stop_Waits_For_Scheduled_Work_Too()
    {
        var server = NewServer(1);
        server.Start();
        var steps = new ConcurrentQueue<int>();
        var thread = _created[0];
        thread.Add(new DelegateStep(() =>
        {
            foreach (var number in Enumerable.Range(1, 5))
            {
                thread.Schedule(new DelegateStep(() => steps.Enqueue(number)));
            }
        }));

        await server.SoftStop().WaitAsync(Timeout);

        Assert.Equal([1, 2, 3, 4, 5], steps.ToArray());
    }

    private sealed class Endless(IServerThread thread, Action tick) : Lib.ICommand
    {
        public void Execute()
        {
            tick();
            thread.Schedule(this);
        }
    }

    [Fact]
    public async Task Hard_Stop_Ends_Threads_That_Would_Never_Drain()
    {
        var server = NewServer(2);
        var ticks = 0;
        server.Start();
        foreach (var thread in _created)
        {
            var current = thread;
            current.Add(new DelegateStep(() => current.Schedule(new Endless(current, () => Interlocked.Increment(ref ticks)))));
        }
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref ticks) > 10, Timeout));

        var stopped = server.HardStop();

        await stopped.WaitAsync(Timeout);
        Assert.False(server.IsRunning);
        Assert.Equal(0, _ring.Count);
        var after = Volatile.Read(ref ticks);
        Thread.Sleep(100);
        Assert.Equal(after, Volatile.Read(ref ticks));
    }

    [Fact]
    public async Task Stopped_Threads_Leave_The_Ring_So_Nothing_Is_Routed_To_Them()
    {
        var server = NewServer(2);
        server.Start();

        var stopped = server.SoftStop();

        await stopped.WaitAsync(Timeout);
        Assert.Throws<InvalidOperationException>(() => _ring.Find("Battle"));
    }

    [Fact]
    public async Task Server_That_Was_Never_Started_Can_Be_Stopped()
    {
        var server = NewServer(2);
        server.AddThread();

        await server.HardStop().WaitAsync(Timeout);

        Assert.False(server.IsRunning);
        Assert.Equal(0, _ring.Count);
    }

    [Fact]
    public async Task Stopped_Server_Cannot_Be_Used_Again()
    {
        var server = NewServer(1);
        server.Start();
        await server.SoftStop().WaitAsync(Timeout);

        Assert.Throws<InvalidOperationException>(() => server.Start());
        Assert.Throws<InvalidOperationException>(() => server.AddThread());
        Assert.Throws<InvalidOperationException>(() => { _ = server.SoftStop(); });
        Assert.Throws<InvalidOperationException>(() => { _ = server.HardStop(); });
    }

    [Fact]
    public async Task Ensure_Running_Passes_Only_While_The_Server_Is_Running()
    {
        var server = NewServer(1);
        Assert.Throws<InvalidOperationException>(() => server.EnsureRunning());
        server.Start();

        server.EnsureRunning();

        await server.SoftStop().WaitAsync(Timeout);
        Assert.Throws<InvalidOperationException>(() => server.EnsureRunning());
    }

    [Fact]
    public async Task Server_Is_Stopping_Until_Every_Thread_Has_Finished_And_Refuses_Everything_Meanwhile()
    {
        var server = NewServer(1);
        using var release = new ManualResetEventSlim();
        server.Start();
        _created[0].Add(new DelegateStep(() => release.Wait(Timeout)));

        var stopped = server.SoftStop();

        Assert.False(server.IsRunning);
        Assert.Throws<InvalidOperationException>(() => server.Start());
        Assert.Throws<InvalidOperationException>(() => server.AddThread());
        Assert.Throws<InvalidOperationException>(() => { _ = server.SoftStop(); });
        Assert.Throws<InvalidOperationException>(() => { _ = server.HardStop(); });
        release.Set();
        await stopped.WaitAsync(Timeout);
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task Failure_Of_A_Thread_Is_Reported_By_The_Stop_Task_But_The_Server_Still_Stops()
    {
        var failing = new FailingHandler();
        var thread = new ServerThread(new BlockingCommandQueue(), new BlockingCommandQueue(), new QueueScheduler(),
            failing, _exceptions);
        var server = new Server(_ring, () => thread, t => new SoftStopCommand(t, _exceptions),
            t => new HardStopCommand(t, _exceptions), 1);
        server.Start();
        thread.Add(new DelegateStep(() => throw new InvalidOperationException("boom")));

        var stopped = server.SoftStop();

        await Assert.ThrowsAsync<InvalidOperationException>(() => stopped.WaitAsync(Timeout));
        Assert.False(server.IsRunning);
    }

    private sealed class FailingHandler : IExceptionHandler
    {
        public void Handle(Lib.ICommand command, Exception exception) => throw exception;
    }

    [Fact]
    public async Task Thread_Identifiers_Use_The_Prefix_And_Are_Unique()
    {
        var ids = new List<string>();
        var ring = new RecordingRing(ids);
        var server = new Server(ring, NewThread, t => new SoftStopCommand(t, _exceptions),
            t => new HardStopCommand(t, _exceptions), 2, "Worker");

        server.Start();
        server.AddThread();

        Assert.Equal(["Worker-0", "Worker-1", "Worker-2"], ids);
        await server.SoftStop().WaitAsync(Timeout);
    }

    private sealed class RecordingRing(List<string> ids) : IConsistentHashRing<IServerThread>
    {
        public int Count => ids.Count;

        public void Add(string id, IServerThread node) => ids.Add(id);

        public bool Remove(string id) => true;

        public IServerThread Find(string key) => throw new NotSupportedException();
    }

    [Fact]
    public void Arguments_Are_Validated()
    {
        Lib.ICommand Stop(IServerThread thread) => new SoftStopCommand(thread, _exceptions);

        Assert.Throws<ArgumentNullException>(() => new Server(null!, NewThread, Stop, Stop, 1));
        Assert.Throws<ArgumentNullException>(() => new Server(_ring, null!, Stop, Stop, 1));
        Assert.Throws<ArgumentNullException>(() => new Server(_ring, NewThread, null!, Stop, 1));
        Assert.Throws<ArgumentNullException>(() => new Server(_ring, NewThread, Stop, null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Server(_ring, NewThread, Stop, Stop, 0));
        Assert.Throws<ArgumentException>(() => new Server(_ring, NewThread, Stop, Stop, 1, " "));
    }

    [Fact]
    public async Task Hard_Stop_Overtakes_A_Long_Queue_Of_Waiting_Commands()
    {
        var server = NewServer(2);
        var executed = 0;
        var release = new ManualResetEventSlim();
        server.Start();
        foreach (var thread in _created)
        {
            thread.Add(new DelegateStep(() => release.Wait(Timeout)));
            foreach (var _ in Enumerable.Range(0, 50))
            {
                thread.Add(new DelegateStep(() => Interlocked.Increment(ref executed)));
            }
        }

        var stopped = server.HardStop();
        release.Set();

        await stopped.WaitAsync(Timeout);
        Assert.Equal(0, executed);
        Assert.False(server.IsRunning);
    }

    [Fact]
    public async Task Soft_Stop_Does_Not_Overtake_The_Queue()
    {
        var server = NewServer(2);
        var executed = 0;
        var release = new ManualResetEventSlim();
        server.Start();
        foreach (var thread in _created)
        {
            thread.Add(new DelegateStep(() => release.Wait(Timeout)));
            foreach (var _ in Enumerable.Range(0, 50))
            {
                thread.Add(new DelegateStep(() => Interlocked.Increment(ref executed)));
            }
        }

        var stopped = server.SoftStop();
        release.Set();

        await stopped.WaitAsync(Timeout);
        Assert.Equal(100, executed);
    }
}
