using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameRunnerRegistrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly RecordingExceptionHandler _handler = new();

    public GameRunnerRegistrationTests()
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", ServerThreadDependencies.ExceptionHandler,
            (object[] args) => _handler).Execute();
        new RegisterIoCDependencyBattleContextFactory().Execute();
        new RegisterIoCDependencyGameQuantum(TimeSpan.FromHours(1)).Execute();
        new RegisterIoCDependencyGame().Execute();
        new RegisterIoCDependencyServerThread().Execute();
        new RegisterIoCDependencyGameRunner().Execute();
    }

    [Fact]
    public async Task Message_Posted_Through_The_Registered_Commands_Runs_In_The_Game_Scope_On_The_Thread()
    {
        var thread = Ioc.Resolve<IServerThread>(ServerThreadDependencies.Create);
        var game = Ioc.Resolve<IGame>(GameDependencies.Create, "Battle");
        var runner = Ioc.Resolve<IGameRunner>(GameDependencies.Runner, game, thread);
        var seen = new System.Collections.Concurrent.ConcurrentQueue<(int Thread, IBattleContext Context)>();
        var message = new DelegateStep(() => seen.Enqueue((Environment.CurrentManagedThreadId,
            Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context))));
        thread.Add(Ioc.Resolve<Lib.ICommand>(GameDependencies.Post, runner, message));
        thread.Add(Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, thread));

        await thread.Start().WaitAsync(Timeout);

        var (threadId, context) = Assert.Single(seen.ToArray());
        Assert.Equal(thread.ThreadId, threadId);
        Assert.NotNull(context);
    }

    [Fact]
    public void Runner_Is_New_Every_Time()
    {
        var thread = Ioc.Resolve<IServerThread>(ServerThreadDependencies.Create);
        var game = Ioc.Resolve<IGame>(GameDependencies.Create, "Battle");

        Assert.NotSame(Ioc.Resolve<IGameRunner>(GameDependencies.Runner, game, thread),
            Ioc.Resolve<IGameRunner>(GameDependencies.Runner, game, thread));
    }

    [Fact]
    public void Runner_Needs_A_Game_And_A_Thread()
    {
        var thread = Ioc.Resolve<IServerThread>(ServerThreadDependencies.Create);
        var game = Ioc.Resolve<IGame>(GameDependencies.Create, "Battle");

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameRunner>(GameDependencies.Runner));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameRunner>(GameDependencies.Runner, game));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<IGameRunner>(GameDependencies.Runner, thread, game));
    }

    [Fact]
    public void Post_Needs_A_Runner_And_A_Command()
    {
        var thread = Ioc.Resolve<IServerThread>(ServerThreadDependencies.Create);
        var game = Ioc.Resolve<IGame>(GameDependencies.Create, "Battle");
        var runner = Ioc.Resolve<IGameRunner>(GameDependencies.Runner, game, thread);

        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(GameDependencies.Post, runner));
        Assert.Throws<ArgumentException>(() => Ioc.Resolve<Lib.ICommand>(GameDependencies.Post, "runner", runner));
    }
}
