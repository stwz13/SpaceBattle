using System.Collections.Concurrent;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ServerThreadTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly RecordingExceptionHandler _handler = new();
    private readonly SpyCommandQueue _inbox = new(new BlockingCommandQueue());
    private readonly ExceptionFactory _exceptions = new();
    private readonly ConcurrentQueue<string> _log = new();
    private readonly ServerThread _thread;

    public ServerThreadTests()
    {
        _thread = new ServerThread(_inbox, new BlockingCommandQueue(), new QueueScheduler(), _handler, _exceptions);
    }

    private sealed class Slice(IServerThread thread, ConcurrentQueue<string> log, int total) : Lib.ICommand
    {
        private int _done;

        public void Execute()
        {
            _done++;
            log.Enqueue("s" + _done);
            if (_done < total)
            {
                thread.Schedule(this);
            }
        }
    }

    private static DelegateStep Step(Action action) => new(action);

    private DelegateStep Log(string entry) => Step(() => _log.Enqueue(entry));

    private Lib.ICommand HardStop() => new HardStopCommand(_thread, _exceptions);

    private Lib.ICommand SoftStop() => new SoftStopCommand(_thread, _exceptions);

    private static void Finishes(Task task) => Assert.True(task.Wait(Timeout));

    [Fact]
    public void Commands_Run_In_Order_On_The_Thread_Own_Thread()
    {
        var ids = new ConcurrentQueue<int>();
        _thread.Add(Step(() => ids.Enqueue(Environment.CurrentManagedThreadId)));
        _thread.Add(Log("a"));
        _thread.Add(Log("b"));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Equal(["a", "b"], _log.ToArray());
        Assert.Equal([_thread.ThreadId], ids.ToArray());
        Assert.NotEqual(Environment.CurrentManagedThreadId, _thread.ThreadId);
    }

    [Fact]
    public void Idle_Thread_Waits_On_The_Queue_Instead_Of_Spinning_And_Wakes_For_New_Commands()
    {
        var task = _thread.Start();

        Assert.False(task.Wait(TimeSpan.FromMilliseconds(300)));
        Assert.Equal(1, _inbox.Takes);
        Assert.Equal(0, _inbox.TryTakes);

        _thread.Add(Log("late"));
        _thread.Add(SoftStop());

        Finishes(task);
        Assert.Equal(["late"], _log.ToArray());
    }

    [Fact]
    public void Failing_Command_Goes_To_The_Handler_With_Its_Exception_And_The_Thread_Goes_On()
    {
        var failure = new InvalidOperationException("boom");
        var bad = Step(() => throw failure);
        _thread.Add(Log("a"));
        _thread.Add(bad);
        _thread.Add(Log("b"));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Equal(["a", "b"], _log.ToArray());
        var handled = Assert.Single(_handler.Handled.ToArray());
        Assert.Same(bad, handled.Command);
        Assert.Same(failure, handled.Exception);
    }

    [Fact]
    public void Failing_Scheduled_Command_Goes_To_The_Handler_Too()
    {
        var failure = new InvalidOperationException("boom");
        var bad = Step(() => throw failure);
        _thread.Add(Step(() => _thread.Schedule(bad)));
        _thread.Add(Log("a"));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Same(failure, Assert.Single(_handler.Handled.ToArray()).Exception);
        Assert.Equal(["a"], _log.ToArray());
    }

    [Fact]
    public void Failing_Handler_Ends_The_Thread_With_Its_Exception()
    {
        var failure = new InvalidOperationException("handler failed");
        _handler.Reaction = (_, _) => throw failure;
        _thread.Add(Step(() => throw new InvalidOperationException("boom")));

        var task = _thread.Start();

        var error = Assert.Throws<AggregateException>(() => task.Wait(Timeout));
        Assert.Same(failure, error.InnerException);
    }

    [Fact]
    public void HardStop_Ends_The_Thread_Without_Running_The_Rest_Of_The_Queue()
    {
        _thread.Add(Log("a"));
        _thread.Add(HardStop());
        _thread.Add(Log("b"));
        _thread.Add(Log("c"));

        Finishes(_thread.Start());

        Assert.Equal(["a"], _log.ToArray());
        Assert.Throws<InvalidOperationException>(() => _thread.Add(Log("late")));
    }

    [Fact]
    public void HardStop_Drops_Scheduled_Work_Too()
    {
        _thread.Add(Step(() => _thread.Schedule(new Slice(_thread, _log, 5))));
        _thread.Add(HardStop());

        Finishes(_thread.Start());

        Assert.Empty(_log.ToArray());
    }

    [Fact]
    public void SoftStop_Runs_Everything_Already_Queued_And_Then_Ends()
    {
        _thread.Add(Log("a"));
        _thread.Add(SoftStop());
        _thread.Add(Log("b"));
        _thread.Add(Log("c"));

        Finishes(_thread.Start());

        Assert.Equal(["a", "b", "c"], _log.ToArray());
        Assert.Throws<InvalidOperationException>(() => _thread.Add(Log("late")));
    }

    [Fact]
    public void SoftStop_Waits_For_Scheduled_Work_To_Finish()
    {
        _thread.Add(Step(() => _thread.Schedule(new Slice(_thread, _log, 4))));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Equal(["s1", "s2", "s3", "s4"], _log.ToArray());
    }

    [Fact]
    public void Soft_Stopped_Thread_Ends_Even_If_Nothing_Is_Left()
    {
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Empty(_log.ToArray());
    }

    [Fact]
    public void Stop_Commands_Refuse_To_Run_Outside_The_Thread_They_Stop()
    {
        Assert.Throws<InvalidOperationException>(HardStop().Execute);
        Assert.Throws<InvalidOperationException>(SoftStop().Execute);

        var task = _thread.Start();
        _thread.Add(Log("still working"));
        Assert.Throws<InvalidOperationException>(HardStop().Execute);
        Assert.Throws<InvalidOperationException>(SoftStop().Execute);
        _thread.Add(Log("and working"));
        _thread.Add(SoftStop());

        Finishes(task);
        Assert.Equal(["still working", "and working"], _log.ToArray());
    }

    [Fact]
    public void Long_Running_Commands_Take_Turns_With_New_Commands()
    {
        _thread.Add(Step(() =>
        {
            _log.Enqueue("start");
            _thread.Schedule(new Slice(_thread, _log, 3));
        }));
        _thread.Add(Log("A"));
        _thread.Add(Log("B"));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Equal(["start", "A", "s1", "B", "s2", "s3"], _log.ToArray());
    }

    [Fact]
    public void Scheduled_Work_Does_Not_Wait_For_New_Commands()
    {
        using var done = new ManualResetEventSlim();
        _thread.Add(Step(() => _thread.Schedule(new Slice(_thread, _log, 50))));
        _thread.Add(Step(() => _thread.Schedule(Step(done.Set))));
        var task = _thread.Start();

        Assert.True(done.Wait(Timeout));

        _thread.Add(SoftStop());
        Finishes(task);
        Assert.Equal(50, _log.Count);
    }

    [Fact]
    public void Behaviour_Can_Be_Replaced_For_The_Next_Commands()
    {
        var turns = 0;
        _thread.Add(Step(() =>
        {
            _log.Enqueue("switching");
            _thread.UpdateBehaviour(() =>
            {
                turns++;
                if (turns == 3)
                {
                    _thread.HardStop();
                }
            });
        }));
        _thread.Add(Log("never"));

        Finishes(_thread.Start());

        Assert.Equal(3, turns);
        Assert.Equal(["switching"], _log.ToArray());
    }

    [Fact]
    public void Many_Producers_Can_Add_Commands_At_The_Same_Time()
    {
        var executed = 0;
        var task = _thread.Start();

        Task.WaitAll(Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                foreach (var command in Enumerable.Range(0, 250).Select(__ => Step(() => executed++)))
                {
                    _thread.Add(command);
                }
            }))
            .ToArray());
        _thread.Add(SoftStop());

        Finishes(task);
        Assert.Equal(1000, executed);
    }

    [Fact]
    public void Thread_Can_Be_Started_Only_Once()
    {
        var task = _thread.Start();

        Assert.Throws<InvalidOperationException>(() => { _ = _thread.Start(); });

        _thread.Add(SoftStop());
        Finishes(task);
    }

    [Fact]
    public void Thread_Id_Is_Unknown_Until_It_Runs()
    {
        Assert.Equal(0, _thread.ThreadId);
    }

    [Fact]
    public void Invalid_Arguments_Are_Rejected()
    {
        var queue = new BlockingCommandQueue();
        var scheduler = new QueueScheduler();

        Assert.Throws<ArgumentNullException>(() => new ServerThread(null!, queue, scheduler, _handler, _exceptions));
        Assert.Throws<ArgumentNullException>(() => new ServerThread(queue, null!, scheduler, _handler, _exceptions));
        Assert.Throws<ArgumentNullException>(() => new ServerThread(queue, queue, null!, _handler, _exceptions));
        Assert.Throws<ArgumentNullException>(() => new ServerThread(queue, queue, scheduler, null!, _exceptions));
        Assert.Throws<ArgumentNullException>(() => new ServerThread(queue, queue, scheduler, _handler, null!));
        Assert.Throws<ArgumentNullException>(() => _thread.UpdateBehaviour(null!));
        Assert.Throws<ArgumentNullException>(() => _thread.Add(null!));
        Assert.Throws<ArgumentNullException>(() => _thread.Schedule(null!));
    }

    [Fact]
    public void Interrupting_Command_Runs_Before_Everything_Already_In_The_Queue()
    {
        var gate = new ManualResetEventSlim();
        _thread.Add(Step(() => gate.Wait(Timeout)));
        _thread.Add(Log("a"));
        _thread.Add(Log("b"));
        var task = _thread.Start();

        _thread.Interrupt(Log("urgent"));
        gate.Set();
        _thread.Add(SoftStop());

        Finishes(task);
        Assert.Equal(["urgent", "a", "b"], _log.ToArray());
    }

    [Fact]
    public void Interrupting_Hard_Stop_Drops_Everything_Waiting_In_The_Queue_And_The_Scheduler()
    {
        var gate = new ManualResetEventSlim();
        var executed = 0;
        _thread.Add(Step(() =>
        {
            _thread.Schedule(Step(() => Interlocked.Increment(ref executed)));
            gate.Wait(Timeout);
        }));
        foreach (var _ in Enumerable.Range(0, 100))
        {
            _thread.Add(Step(() => Interlocked.Increment(ref executed)));
        }
        var task = _thread.Start();

        _thread.Interrupt(HardStop());
        gate.Set();

        Finishes(task);
        Assert.Equal(0, executed);
    }

    [Fact]
    public void Interrupting_Command_Wakes_An_Idle_Thread()
    {
        var task = _thread.Start();
        Assert.False(task.Wait(TimeSpan.FromMilliseconds(200)));

        _thread.Interrupt(HardStop());

        Finishes(task);
    }

    [Fact]
    public void Interrupting_A_Thread_That_Is_Stopping_Is_Rejected()
    {
        _thread.Add(SoftStop());
        Finishes(_thread.Start());

        Assert.Throws<InvalidOperationException>(() => _thread.Interrupt(Log("late")));
    }

    [Fact]
    public void TryAdd_Refuses_Commands_While_The_Inbox_Is_Full_But_Plain_And_Urgent_Adds_Still_Work()
    {
        var inbox = new BlockingCommandQueue(1);
        var thread = new ServerThread(inbox, new BlockingCommandQueue(), new QueueScheduler(), _handler, _exceptions);

        Assert.True(thread.TryAdd(Log("first")));
        Assert.False(thread.TryAdd(Log("second")));
        thread.Add(Log("third"));
        thread.Interrupt(Log("urgent"));

        Assert.Equal(3, inbox.Count);
    }
}
