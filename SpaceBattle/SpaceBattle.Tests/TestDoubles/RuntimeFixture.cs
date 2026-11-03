using System.Collections.Concurrent;
using System.Text.Json;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class RuntimeFixture : IAsyncDisposable
{
    public GameRuntime Runtime { get; }
    public IGameScope Scope { get; }
    public ConcurrentQueue<(string Game, int Value)> Seen { get; } = new();
    public RecordingExceptionHandler Errors { get; } = new();
    public RuntimeFixture(int threads = 2, int capacity = 1000)
    {
        new InitCommand().Execute();
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        new RegisterIoCDependencyExceptionFactory().Execute();
        Register(ServerThreadDependencies.ExceptionHandler, _ => Errors);
        new RegisterIoCDependencyBattleContextFactory().Execute();
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyGameQuantum(TimeSpan.Zero).Execute();
        new RegisterIoCDependencyGame().Execute();
        new RegisterIoCDependencyServerThread(capacity).Execute();
        new RegisterIoCDependencyRouting(64).Execute();
        new RegisterIoCDependencyGameRunner().Execute();
        new RegisterIoCDependencyMessages().Execute();
        new RegisterIoCDependencyServer(threads).Execute();
        new RegisterIoCDependencyGameSnapshot().Execute();
        new RegisterIoCDependencyGameItems().Execute();
        new RegisterIoCDependencyItemKind("Ship", ["Fuel"]).Execute();
        Register(MessageDependencies.CommandKey("Record"), args => new CallbackCommand(() =>
            Seen.Enqueue((Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Id,
                ((IMessage)args[0]).Args.GetInt32()))));
        new RegisterIoCDependencyManagedGames().Execute();
        Scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
        Runtime = Ioc.Resolve<GameRuntime>("Server.Games");
        Runtime.Server.Start();
    }
    public T Resolve<T>(string key, params object[] args)
    {
        using var entered = Scope.Enter();
        return Ioc.Resolve<T>(key, args);
    }
    public static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
    public static Message Message(string id, int value) => new(id, "Record", JsonSerializer.SerializeToElement(value));
    public async ValueTask DisposeAsync()
    {
        if (Runtime.Server.State == ServerState.Running)
            await Runtime.Server.SoftStop().WaitAsync(TimeSpan.FromSeconds(10));
    }
}
