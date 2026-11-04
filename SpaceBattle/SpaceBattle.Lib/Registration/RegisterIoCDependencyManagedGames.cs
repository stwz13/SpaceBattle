using System.Collections.Concurrent;
using System.Text.Json;
using App;

namespace SpaceBattle.Lib;

/// <summary>Registers managed games in a child scope, preserving the original registrations.</summary>
public sealed class RegisterIoCDependencyManagedGames : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
        var server = Ioc.Resolve<IServer>(ServerDependencies.Instance);
        IMessageInterpreter interpreter = new IoCMessageInterpreter();
        Register("Messages.Interpreter", _ => interpreter);
        Register(GameDependencies.Create, args => args is [string id] ? Create(id) : throw new ArgumentException("Expected a game id."));
        var runtime = new GameRuntime(server, new ConcurrentDictionary<string, IGameHost>(), new SemaphoreSlim(1, 1),
            id => InScope(scope, () => Ioc.Resolve<IManagedGame>(GameDependencies.Create, id)),
            json => InScope(scope, () => Ioc.Resolve<IManagedGame>(SerializationDependencies.Restore, json)),
            (game, target) => InScope(scope, () =>
            {
                var thread = target ?? Ioc.Resolve<IServerThread>(RoutingDependencies.Thread, game.Id);
                var runner = new GameRunner(game, thread);
                return new ManagedGameHost(game, thread, runner, new object(),
                    (owner, message) => new PostToGameCommand(owner, new InterpretMessageCommand(
                        InScope(scope, () => Ioc.Resolve<IMessageInterpreter>("Messages.Interpreter")), Normalize(message))),
                    Invoke, (value, sink) => Ioc.Resolve<Lib.ICommand>(SerializationDependencies.Snapshot, value, sink),
                    (owner, targetThread, action) =>
                    {
                        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        targetThread.Add(new PostToGameCommand(owner, new CompletionCommand(action, completion)));
                        return completion.Task;
                    }, owner => ((GameRunner)owner).Wake());
            }));
        var serializers = Ioc.Resolve<List<ICommandSerializer>>(SerializationDependencies.CommandSerializers);
        serializers[0] = new MessageCommandSerializer(new DelegatingMessageInterpreter(
            () => InScope(scope, () => Ioc.Resolve<IMessageInterpreter>("Messages.Interpreter"))));
        Register("Server.Games", _ => runtime);
        Register(GameDependencies.Registry, _ => runtime);
        Register(GameDependencies.Start, args => args switch
        {
            [string id] => runtime.Start(id),
            [IManagedGame game] => runtime.Start(game),
            _ => throw new ArgumentException("Expected an id or a managed game.")
        });
        Register("Games.StartRestored", args => args is [IManagedGame game] ? runtime.Start(game)
            : throw new ArgumentException("Expected a restored managed game."));
        Register(MessageDependencies.Receiver, _ => new MessageReceiver(Ioc.Resolve<IMessageParser>(MessageDependencies.Parser), runtime));
        Register("Server.SnapshotAndStop", args => args is [ISnapshotStore store] ? runtime.SnapshotAndStop(store)
            : throw new ArgumentException("Expected a snapshot store."));
        Register("Server.Restore", args => args is [ISnapshotStore store] ? runtime.Restore(store)
            : throw new ArgumentException("Expected a snapshot store."));
        Register("Snapshots.Memory", _ => new MemorySnapshotStore(new ConcurrentDictionary<string, string>()));
        Register("Commands.Server.SnapshotAndStop", args => args is [ISnapshotStore store]
            ? new SnapshotAndStopCommand(runtime, store) : throw new ArgumentException("Expected a snapshot store."));
    }

    private static ManagedGame Create(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var context = Ioc.Resolve<IBattleContextFactory>(BattleContextDependencies.Factory).Create(id);
        var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        using (scope.Enter()) new RegisterIoCDependencyBattleContext(context).Execute();
        return new ManagedGame(new Game(id, new GameQueue(), scope, Ioc.Resolve<IQuantum>(GameDependencies.Quantum)), scope);
    }

    internal static Task Invoke(IServerThread thread, Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thread.Add(new CompletionCommand(action, completion));
        return completion.Task;
    }

    private static IMessage Normalize(IMessage message) => message.Args.ValueKind == JsonValueKind.Undefined
        ? new Message(message.GameId, message.Command, JsonSerializer.SerializeToElement<object?>(null)) : message;

    internal static T InScope<T>(IGameScope scope, Func<T> action)
    {
        using var entered = scope.Enter();
        return action();
    }
    internal static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
