using System.Collections.Concurrent;
using System.Text.Json;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MessagesRegistrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly RecordingExceptionHandler _handler = new();
    private readonly ConcurrentQueue<(string Game, int Thread, string Text)> _seen = new();

    public MessagesRegistrationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
        Register(ServerThreadDependencies.ExceptionHandler, _ => _handler);
        new RegisterIoCDependencyBattleContextFactory().Execute();
        new RegisterIoCDependencyGameQuantum(TimeSpan.FromHours(1)).Execute();
        new RegisterIoCDependencyGame().Execute();
        new RegisterIoCDependencyServerThread().Execute();
        new RegisterIoCDependencyGameRunner().Execute();
        new RegisterIoCDependencyRouting(16).Execute();
        new RegisterIoCDependencyMessages().Execute();
        Register(MessageDependencies.CommandKey("Echo"), args => new DelegateStep(() =>
        {
            var message = (IMessage)args[0];
            var context = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context);
            Assert.NotNull(context);
            _seen.Enqueue((message.GameId, Environment.CurrentManagedThreadId, message.Args.GetProperty("text").GetString()!));
        }));
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();

    private static IServerThread AddThread(string id)
    {
        var thread = Ioc.Resolve<IServerThread>(ServerThreadDependencies.Create);
        Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring).Add(id, thread);
        return thread;
    }

    private static void Receive(string text) =>
        Ioc.Resolve<IMessageReceiver>(MessageDependencies.Receiver).Receive(text);

    private static string Echo(string game, string text) =>
        JsonSerializer.Serialize(new { gameId = game, command = "Echo", args = new { text } });

    [Fact]
    public void Message_Reaches_The_Game_On_Its_Thread_Inside_The_Game_Scope()
    {
        var thread = AddThread("T1");
        Ioc.Resolve<IGameHost>(GameDependencies.Start, "Battle");
        var task = thread.Start();

        Receive(Echo("Battle", "hello"));
        Assert.True(SpinWait.SpinUntil(() => !_seen.IsEmpty, Timeout));
        thread.Add(Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, thread));

        Assert.True(task.Wait(Timeout));
        var (game, threadId, text) = Assert.Single(_seen.ToArray());
        Assert.Equal("Battle", game);
        Assert.Equal(thread.ThreadId, threadId);
        Assert.Equal("hello", text);
        Assert.Empty(_handler.Handled.ToArray());
    }

    [Fact]
    public void Messages_Of_A_Game_Keep_Their_Order()
    {
        var thread = AddThread("T1");
        Ioc.Resolve<IGameHost>(GameDependencies.Start, "Battle");
        var task = thread.Start();

        foreach (var index in Enumerable.Range(0, 50))
        {
            Receive(Echo("Battle", index.ToString()));
        }
        thread.Add(Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, thread));

        Assert.True(task.Wait(Timeout));
        Assert.Equal(Enumerable.Range(0, 50).Select(index => index.ToString()), _seen.Select(entry => entry.Text));
    }

    [Fact]
    public void Games_Are_Spread_Over_Threads_By_The_Ring_And_Messages_Follow_Their_Game()
    {
        var first = AddThread("T1");
        var second = AddThread("T2");
        var games = Enumerable.Range(0, 40).Select(index => $"Battle-{index}").ToArray();
        foreach (var game in games)
        {
            Ioc.Resolve<IGameHost>(GameDependencies.Start, game);
        }
        var tasks = new[] { first.Start(), second.Start() };

        foreach (var game in games)
        {
            Receive(Echo(game, "x"));
            Receive(Echo(game, "y"));
        }
        Assert.True(SpinWait.SpinUntil(() => _seen.Count == 80, Timeout));
        first.Add(Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, first));
        second.Add(Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, second));

        Assert.True(Task.WaitAll(tasks, Timeout));
        var threadsOfGame = _seen.GroupBy(entry => entry.Game).ToDictionary(group => group.Key,
            group => group.Select(entry => entry.Thread).Distinct().ToArray());
        Assert.All(threadsOfGame.Values, threads => Assert.Single(threads));
        Assert.Equal(2, threadsOfGame.Values.Select(threads => threads[0]).Distinct().Count());
        Assert.All(games, game => Assert.Equal(
            Ioc.Resolve<IServerThread>(RoutingDependencies.Thread, game).ThreadId, threadsOfGame[game][0]));
    }

    [Fact]
    public void Unknown_Command_Is_Reported_To_The_Thread_Handler_And_Does_Not_Stop_The_Game()
    {
        var thread = AddThread("T1");
        Ioc.Resolve<IGameHost>(GameDependencies.Start, "Battle");
        var task = thread.Start();

        Receive("""{"gameId":"Battle","command":"IoC.Register"}""");
        Receive(Echo("Battle", "still alive"));
        thread.Add(Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, thread));

        Assert.True(task.Wait(Timeout));
        Assert.Single(_handler.Handled.ToArray());
        Assert.Equal("still alive", Assert.Single(_seen.ToArray()).Text);
    }

    [Fact]
    public void Message_Cannot_Reach_Dependencies_Outside_The_Messages_Namespace()
    {
        var thread = AddThread("T1");
        Ioc.Resolve<IGameHost>(GameDependencies.Start, "Battle");
        var task = thread.Start();
        var reached = false;
        Register("Secret", _ => new DelegateStep(() => reached = true));

        Receive("""{"gameId":"Battle","command":"Secret"}""");
        thread.Add(Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, thread));

        Assert.True(task.Wait(Timeout));
        Assert.False(reached);
        Assert.Single(_handler.Handled.ToArray());
    }

    [Fact]
    public void Message_For_A_Game_That_Was_Not_Started_Is_Rejected()
    {
        AddThread("T1");

        Assert.Throws<KeyNotFoundException>(() => Receive(Echo("nobody", "hi")));
    }

    [Fact]
    public void Same_Game_Cannot_Be_Started_Twice()
    {
        AddThread("T1");
        Ioc.Resolve<IGameHost>(GameDependencies.Start, "Battle");

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameHost>(GameDependencies.Start, "Battle"));
    }

    [Fact]
    public void Registered_Services_Take_No_Stray_Arguments_And_Start_Needs_An_Identifier()
    {
        AddThread("T1");

        Assert.Same(Ioc.Resolve<IGameRegistry>(GameDependencies.Registry), Ioc.Resolve<IGameRegistry>(GameDependencies.Registry));
        Assert.IsType<JsonMessageParser>(Ioc.Resolve<IMessageParser>(MessageDependencies.Parser));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameRegistry>(GameDependencies.Registry, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IMessageParser>(MessageDependencies.Parser, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IMessageReceiver>(MessageDependencies.Receiver, 1));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameHost>(GameDependencies.Start));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameHost>(GameDependencies.Start, 5));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameHost>(GameDependencies.Start, " "));
    }
}
