using System.Collections.Concurrent;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameRunnerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentQueue<string> _log = new();
    private readonly RecordingExceptionHandler _handler = new();
    private readonly ServerThread _thread;

    public GameRunnerTests()
    {
        _thread = new ServerThread(new BlockingCommandQueue(), new BlockingCommandQueue(), new QueueScheduler(),
            _handler, new ExceptionFactory());
    }

    private sealed class SlicedGame(string id, ConcurrentQueue<string> log, int perExecution) : IGame
    {
        private readonly Queue<Lib.ICommand> _commands = new();

        public string Id { get; } = id;

        public bool HasCommands => _commands.Count > 0;

        public IReadOnlyCollection<Lib.ICommand> Pending => _commands.ToArray();

        public void Post(Lib.ICommand command) => _commands.Enqueue(command);

        public void Execute()
        {
            log.Enqueue(Id + ":slice");
            foreach (var command in Enumerable.Range(0, perExecution).Where(_ => HasCommands).Select(_ => _commands.Dequeue()))
            {
                command.Execute();
            }
        }
    }

    private SlicedGame Game(string id, int perExecution) => new(id, _log, perExecution);

    private DelegateStep Log(string entry) => new(() => _log.Enqueue(entry));

    private Lib.ICommand SoftStop() => new SoftStopCommand(_thread, new ExceptionFactory());

    private static void Finishes(Task task) => Assert.True(task.Wait(Timeout));

    [Fact]
    public void Posted_Command_Runs_On_The_Thread_And_The_Game_Goes_Idle()
    {
        var game = Game("A", 5);
        var runner = new GameRunner(game, _thread);
        var ids = new ConcurrentQueue<int>();
        _thread.Add(new PostToGameCommand(runner, new DelegateStep(() => ids.Enqueue(Environment.CurrentManagedThreadId))));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Equal([_thread.ThreadId], ids.ToArray());
        Assert.Equal(["A:slice"], _log.ToArray());
        Assert.False(game.HasCommands);
    }

    [Fact]
    public void Game_Reschedules_Itself_While_It_Has_Commands()
    {
        var game = Game("A", 2);
        var runner = new GameRunner(game, _thread);
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            _thread.Add(new PostToGameCommand(runner, Log(name)));
        }
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Equal(["A:slice", "a", "b", "A:slice", "c", "d", "A:slice", "e"], _log.ToArray());
    }

    [Fact]
    public void Idle_Game_Is_Woken_Up_By_A_New_Command()
    {
        var game = Game("A", 5);
        var runner = new GameRunner(game, _thread);
        var task = _thread.Start();
        _thread.Add(new PostToGameCommand(runner, Log("first")));
        SpinWait.SpinUntil(() => _log.Contains("first"), Timeout);
        Assert.False(game.HasCommands);

        _thread.Add(new PostToGameCommand(runner, Log("second")));
        SpinWait.SpinUntil(() => _log.Contains("second"), Timeout);
        _thread.Add(SoftStop());

        Finishes(task);
        Assert.Equal(["A:slice", "first", "A:slice", "second"], _log.ToArray());
    }

    [Fact]
    public void Command_Posted_While_The_Game_Is_Scheduled_Does_Not_Schedule_It_Twice()
    {
        var game = Game("A", 1);
        var runner = new GameRunner(game, _thread);
        _thread.Add(new PostToGameCommand(runner, Log("a")));
        _thread.Add(new PostToGameCommand(runner, Log("b")));
        _thread.Add(new PostToGameCommand(runner, Log("c")));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Equal(["a", "b", "c"], _log.ToArray().Where(e => e != "A:slice").ToArray());
        Assert.Equal(3, _log.ToArray().Count(e => e == "A:slice"));
    }

    [Fact]
    public void Games_On_One_Thread_Take_Turns()
    {
        var first = new GameRunner(Game("A", 1), _thread);
        var second = new GameRunner(Game("B", 1), _thread);
        _thread.Add(new DelegateStep(() =>
        {
            first.Post(Log("a1"));
            first.Post(Log("a2"));
            second.Post(Log("b1"));
            second.Post(Log("b2"));
        }));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        var order = _log.ToArray().Where(e => e is "a1" or "a2" or "b1" or "b2").ToArray();
        Assert.Equal(["a1", "b1", "a2", "b2"], order);
    }

    [Fact]
    public void Failing_Game_Command_Goes_To_The_Handler_And_The_Game_Keeps_Running()
    {
        var game = Game("A", 1);
        var runner = new GameRunner(game, _thread);
        var failure = new InvalidOperationException("boom");
        _thread.Add(new PostToGameCommand(runner, new DelegateStep(() => throw failure)));
        _thread.Add(new PostToGameCommand(runner, Log("after")));
        _thread.Add(SoftStop());

        Finishes(_thread.Start());

        Assert.Same(failure, Assert.Single(_handler.Handled.ToArray()).Exception);
        Assert.Contains("after", _log.ToArray());
        Assert.False(game.HasCommands);
    }

    [Fact]
    public void Arguments_Are_Validated()
    {
        var game = Game("A", 1);

        Assert.Throws<ArgumentNullException>(() => new GameRunner(null!, _thread));
        Assert.Throws<ArgumentNullException>(() => new GameRunner(game, null!));
        Assert.Throws<ArgumentNullException>(() => new GameRunner(game, _thread).Post(null!));
        Assert.Throws<ArgumentNullException>(() => new PostToGameCommand(null!, Log("a")));
        Assert.Throws<ArgumentNullException>(() => new PostToGameCommand(new GameRunner(game, _thread), null!));
    }
}
