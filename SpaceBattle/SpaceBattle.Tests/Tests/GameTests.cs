using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameTests
{
    private readonly List<string> _log = [];
    private readonly FakeQuantum _quantum = new();
    private readonly FakeScope _scope;
    private readonly GameQueue _queue = new();
    private readonly Game _game;

    public GameTests()
    {
        _scope = new FakeScope(_log);
        _game = new Game("Battle", _queue, _scope, _quantum);
    }

    private sealed class FakeQuantum : IQuantum
    {
        public int StartCount { get; private set; }
        public Func<bool> Over { get; set; } = () => false;
        public bool IsOver => Over();
        public void Start() => StartCount++;
    }

    private sealed class FakeScope(List<string> log) : IGameScope
    {
        public int Entered { get; private set; }
        public int Left { get; private set; }

        public IDisposable Enter()
        {
            Entered++;
            log.Add("enter");
            return new Leave(() =>
            {
                Left++;
                log.Add("leave");
            });
        }

        private sealed class Leave(Action leave) : IDisposable
        {
            public void Dispose() => leave();
        }
    }

    private sealed class Step(List<string> log, string name, Action? action = null) : Lib.ICommand
    {
        public void Execute()
        {
            log.Add(name);
            action?.Invoke();
        }
    }

    private Step Command(string name, Action? action = null) => new(_log, name, action);

    [Fact]
    public void Game_Exposes_Its_Identifier()
    {
        Assert.Equal("Battle", _game.Id);
    }

    [Fact]
    public void Game_Without_Commands_Does_Nothing()
    {
        Assert.False(_game.HasCommands);

        _game.Execute();

        Assert.Equal(["enter", "leave"], _log);
    }

    [Fact]
    public void Commands_Run_In_The_Order_They_Were_Posted()
    {
        _game.Post(Command("a"));
        _game.Post(Command("b"));
        _game.Post(Command("c"));
        Assert.True(_game.HasCommands);

        _game.Execute();

        Assert.Equal(["enter", "a", "b", "c", "leave"], _log);
        Assert.False(_game.HasCommands);
    }

    [Fact]
    public void Commands_Run_Inside_The_Game_Scope()
    {
        _game.Post(Command("a", () => Assert.Equal(1, _scope.Entered - _scope.Left)));

        _game.Execute();

        Assert.Equal(1, _scope.Entered);
        Assert.Equal(1, _scope.Left);
    }

    [Fact]
    public void Execution_Stops_When_The_Quantum_Is_Over_And_Resumes_Next_Time()
    {
        var executed = 0;
        _quantum.Over = () => executed >= 2;
        _game.Post(Command("a", () => executed++));
        _game.Post(Command("b", () => executed++));
        _game.Post(Command("c", () => executed++));
        _game.Post(Command("d", () => executed++));

        _game.Execute();

        Assert.Equal(["enter", "a", "b", "leave"], _log);
        Assert.True(_game.HasCommands);

        executed = 0;
        _game.Execute();

        Assert.Equal(["enter", "a", "b", "leave", "enter", "c", "d", "leave"], _log);
        Assert.False(_game.HasCommands);
    }

    [Fact]
    public void Quantum_Is_Started_On_Every_Execution_Before_The_First_Command()
    {
        var startsSeenByCommand = -1;
        _game.Post(Command("a", () => startsSeenByCommand = _quantum.StartCount));

        _game.Execute();
        _game.Execute();

        Assert.Equal(1, startsSeenByCommand);
        Assert.Equal(2, _quantum.StartCount);
    }

    [Fact]
    public void A_Command_Always_Runs_Even_When_The_Quantum_Is_Over_From_The_Start()
    {
        _quantum.Over = () => true;
        _game.Post(Command("a"));
        _game.Post(Command("b"));

        _game.Execute();

        Assert.Equal(["enter", "a", "leave"], _log);
        Assert.True(_game.HasCommands);

        _game.Execute();

        Assert.Equal(["enter", "a", "leave", "enter", "b", "leave"], _log);
        Assert.False(_game.HasCommands);
    }

    [Fact]
    public void Commands_Posted_By_A_Command_Run_After_The_Queued_Ones()
    {
        _game.Post(Command("a", () => _game.Post(Command("late"))));
        _game.Post(Command("b"));

        _game.Execute();

        Assert.Equal(["enter", "a", "b", "late", "leave"], _log);
    }

    [Fact]
    public void Exception_Leaves_The_Scope_And_Keeps_The_Remaining_Commands_Without_Retrying_The_Failed_One()
    {
        var failure = new InvalidOperationException("boom");
        _game.Post(Command("a"));
        _game.Post(Command("bad", () => throw failure));
        _game.Post(Command("c"));

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(_game.Execute));

        Assert.Equal(["enter", "a", "bad", "leave"], _log);
        Assert.Equal(1, _scope.Left);
        Assert.True(_game.HasCommands);

        _game.Execute();

        Assert.Equal(["enter", "a", "bad", "leave", "enter", "c", "leave"], _log);
    }

    [Fact]
    public void Posting_Nothing_Is_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _game.Post(null!));
    }

    [Fact]
    public void Constructor_Rejects_Invalid_Arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new Game(null!, _queue, _scope, _quantum));
        Assert.Throws<ArgumentException>(() => new Game(" ", _queue, _scope, _quantum));
        Assert.Throws<ArgumentNullException>(() => new Game("Battle", null!, _scope, _quantum));
        Assert.Throws<ArgumentNullException>(() => new Game("Battle", _queue, null!, _quantum));
        Assert.Throws<ArgumentNullException>(() => new Game("Battle", _queue, _scope, null!));
    }

    [Fact]
    public void Queue_Gives_Commands_In_Order_And_Rejects_Nothing()
    {
        var queue = new GameQueue();
        var first = Command("1");
        var second = Command("2");

        queue.Enqueue(first);
        queue.Enqueue(second);

        Assert.True(queue.HasCommands);
        Assert.Same(first, queue.Dequeue());
        Assert.Same(second, queue.Dequeue());
        Assert.False(queue.HasCommands);
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
    }
}
