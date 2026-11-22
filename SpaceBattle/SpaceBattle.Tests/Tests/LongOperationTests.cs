using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class LongOperationTests
{
    private readonly object _scope;
    private readonly Game _game;
    private readonly LongOperationFactory _factory = new();

    public LongOperationTests()
    {
        new InitCommand().Execute();
        _scope = Ioc.Resolve<object>("IoC.Scope.Create");
        _game = new Game("battle", new GameQueue(), new GameScope(_scope),
            Mock.Of<IQuantum>(quantum => quantum.IsOver));
    }

    [Fact]
    public void Registered_Macro_Step_Reuses_The_Continuation_At_The_Tail_Of_The_Game_Queue()
    {
        var seen = new List<string>();
        var macroStep = new MacroCommand([
            new DelegateStep(() =>
            {
                Assert.Same(_scope, Ioc.Resolve<object>("IoC.Scope.Current"));
                seen.Add("move");
            }),
            new DelegateStep(() => seen.Add("collisions"))
        ]);
        LongOperationCommands operation;
        using (new GameScope(_scope).Enter())
        {
            new RegisterIoCDependencyLongOperations().Execute();
            operation = Ioc.Resolve<LongOperationCommands>(LongOperationDependencies.Create,
                macroStep, new CommandReceiver(_game.Post));
        }

        _game.Post(operation.Start);
        _game.Execute();
        Assert.Empty(seen);
        var continuation = Assert.Single(_game.Pending);

        _game.Post(new DelegateStep(() => seen.Add("ordinary")));
        _game.Execute();
        Assert.Same(continuation, _game.Pending.Last());
        _game.Execute();
        _game.Execute();

        Assert.Equal(["move", "collisions", "ordinary", "move", "collisions"], seen);
        Assert.Same(continuation, Assert.Single(_game.Pending));
    }

    [Fact]
    public void Stop_Before_The_First_Step_Cancels_The_Queued_Continuation()
    {
        var steps = 0;
        var operation = Create(() => steps++);
        _game.Post(operation.Start);
        _game.Post(operation.Stop);
        _game.Post(operation.Stop);

        _game.Execute();
        _game.Execute();
        _game.Execute();
        Assert.Single(_game.Pending);
        _game.Execute();

        Assert.Equal(0, steps);
        Assert.False(_game.HasCommands);
    }

    [Fact]
    public void Duplicate_Start_Queues_One_Continuation_And_Stop_Cancels_The_Next_Step()
    {
        var steps = 0;
        var operation = Create(() => steps++);
        _game.Post(operation.Start);
        _game.Post(operation.Start);

        _game.Execute();
        var continuation = _game.Pending.Last();
        _game.Execute();
        Assert.Same(continuation, Assert.Single(_game.Pending));
        _game.Post(operation.Stop);
        _game.Execute();
        _game.Execute();
        _game.Execute();

        Assert.Equal(1, steps);
        Assert.False(_game.HasCommands);
    }

    [Fact]
    public void Restart_Uses_A_New_Continuation_And_Leaves_The_Old_One_Cancelled()
    {
        var steps = 0;
        var operation = Create(() => steps++);
        _game.Post(operation.Start);
        _game.Post(operation.Stop);
        _game.Post(operation.Start);

        _game.Execute();
        var old = _game.Pending.Last();
        _game.Execute();
        _game.Execute();
        Assert.Same(old, _game.Pending.First());
        var current = _game.Pending.Last();
        Assert.NotSame(old, current);

        _game.Execute();
        Assert.Equal(0, steps);
        _game.Execute();
        Assert.Equal(1, steps);
        Assert.Same(current, Assert.Single(_game.Pending));
    }

    [Fact]
    public void Two_Operations_With_The_Same_Step_Can_Be_Stopped_Separately()
    {
        var steps = 0;
        var step = new DelegateStep(() => steps++);
        var receiver = new CommandReceiver(_game.Post);
        var first = _factory.Create(step, receiver);
        var second = _factory.Create(step, receiver);
        _game.Post(first.Start);
        _game.Post(second.Start);
        _game.Post(first.Stop);

        _game.Execute();
        _game.Execute();
        var secondContinuation = _game.Pending.Last();
        _game.Execute();
        _game.Execute();
        _game.Execute();

        Assert.Equal(1, steps);
        Assert.Same(secondContinuation, Assert.Single(_game.Pending));
    }

    [Fact]
    public void Stop_During_The_Step_Prevents_Another_Post()
    {
        var steps = 0;
        LongOperationCommands operation = null!;
        operation = Create(() =>
        {
            steps++;
            operation.Stop.Execute();
        });
        _game.Post(operation.Start);

        _game.Execute();
        _game.Execute();

        Assert.Equal(1, steps);
        Assert.False(_game.HasCommands);
    }

    [Fact]
    public void Step_Failure_Stops_The_Operation_And_Allows_Restart()
    {
        var failure = new InvalidOperationException("Step failed.");
        var steps = 0;
        var operation = Create(() =>
        {
            if (++steps == 1)
                throw failure;
        });
        _game.Post(operation.Start);
        _game.Execute();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(_game.Execute));
        Assert.False(_game.HasCommands);
        _game.Post(operation.Start);
        _game.Execute();
        _game.Execute();

        Assert.Equal(2, steps);
        Assert.Single(_game.Pending);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Posting_Failure_Invalidates_The_Continuation_And_Allows_Restart(int failedPost)
    {
        var failure = new InvalidOperationException("Post failed.");
        var posts = 0;
        var steps = 0;
        var receiver = new CommandReceiver(command =>
        {
            _game.Post(command);
            if (++posts == failedPost)
                throw failure;
        });
        var operation = _factory.Create(new DelegateStep(() => steps++), receiver);
        _game.Post(operation.Start);
        if (failedPost == 2)
            _game.Execute();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(_game.Execute));
        Assert.Single(_game.Pending);
        _game.Post(operation.Start);
        _game.Execute();
        Assert.Equal(failedPost - 1, steps);
        _game.Execute();
        _game.Execute();

        Assert.Equal(failedPost, steps);
        Assert.Single(_game.Pending);
    }

    [Fact]
    public void Failure_In_An_Old_Step_Preserves_A_Restart_Requested_By_That_Step()
    {
        var failure = new InvalidOperationException("Old step failed.");
        var steps = 0;
        LongOperationCommands operation = null!;
        operation = Create(() =>
        {
            if (++steps != 1)
                return;

            operation.Stop.Execute();
            operation.Start.Execute();
            throw failure;
        });
        _game.Post(operation.Start);
        _game.Execute();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(_game.Execute));
        Assert.Single(_game.Pending);
        _game.Execute();

        Assert.Equal(2, steps);
        Assert.Single(_game.Pending);
    }

    [Fact]
    public void Registration_Rejects_Invalid_Arguments()
    {
        using var scope = new GameScope(_scope).Enter();
        new RegisterIoCDependencyLongOperations().Execute();
        var step = new EmptyCommand();
        var receiver = new CommandReceiver(_game.Post);

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<LongOperationCommands>(LongOperationDependencies.Create));
        Assert.Throws<ArgumentException>(() =>
            Ioc.Resolve<LongOperationCommands>(LongOperationDependencies.Create, step, step));
        Assert.Throws<ArgumentException>(() =>
            Ioc.Resolve<LongOperationCommands>(LongOperationDependencies.Create, receiver, receiver));
    }

    private LongOperationCommands Create(Action step) =>
        _factory.Create(new DelegateStep(step), new CommandReceiver(_game.Post));
}
