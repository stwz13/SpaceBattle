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
        IoCRegistrar.Register(MessagePipelineDependencies.Interpreter, _ => interpreter);
        IoCRegistrar.Register(GameDependencies.Create, args => args is [string id] ? Create(id) : throw new ArgumentException("Expected a game id."));
        var registry = new ManagedGameRegistry(new ConcurrentDictionary<string, IGameHost>());
        var lifecycle = new SemaphoreSlim(1, 1);
        Func<string, IManagedGame> create =
            id => ScopeExecution.InScope(scope, () => Ioc.Resolve<IManagedGame>(GameDependencies.Create, id));
        Func<string, IManagedGame> restore = json => ScopeExecution.InScope(scope, () => Ioc.Resolve<IManagedGame>(SerializationDependencies.Restore, json));
        Func<IManagedGame, IServerThread?, IManagedGameHost> hostFactory = (game, target) => ScopeExecution.InScope(scope, () =>
            {
                var thread = target ?? Ioc.Resolve<IServerThread>(RoutingDependencies.Thread, game.Id);
                var runner = new GameRunner(game, thread);
                var gate = new object();
                return new ManagedGameHost(game, thread, runner, gate,
                    (owner, message) => new PostToGameCommand(owner, new InterpretMessageCommand(
                        ScopeExecution.InScope(scope, () => Ioc.Resolve<IMessageInterpreter>(MessagePipelineDependencies.Interpreter)), Normalize(message))),
                    Invoke, (value, sink) => Ioc.Resolve<Lib.ICommand>(SerializationDependencies.Snapshot, value, sink),
                    (owner, targetThread, action) =>
                    {
                        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        targetThread.Add(new PostToGameCommand(owner, new CompletionCommand(action, completion)));
                        return completion.Task;
                    }, owner => owner.Wake(), new GameHostLifecycle(gate));
            });
        var games = new GameLifecycle(server, registry, lifecycle, create, hostFactory);
        var snapshots = new GameSnapshotService(server, registry, lifecycle, restore, games);
        var runtime = new GameRuntime(server, registry, games, snapshots);
        IoCRegistrar.Register(RuntimeServiceDependencies.Registry, _ => registry);
        IoCRegistrar.Register(RuntimeServiceDependencies.Lifecycle, _ => lifecycle);
        IoCRegistrar.Register(RuntimeServiceDependencies.Restore, _ => restore);
        IoCRegistrar.Register(RuntimeServiceDependencies.HostFactory, _ => hostFactory);
        var serializers = Ioc.Resolve<List<ICommandSerializer>>(SerializationDependencies.CommandSerializers);
        serializers[0] = new MessageCommandSerializer(new DelegatingMessageInterpreter(
            () => ScopeExecution.InScope(scope, () => Ioc.Resolve<IMessageInterpreter>(MessagePipelineDependencies.Interpreter))));
        IoCRegistrar.Register(RuntimeDependencies.Games, _ => runtime);
        IoCRegistrar.Register(GameDependencies.Registry, _ => runtime);
        IoCRegistrar.Register(GameDependencies.Start, args => args switch
        {
            [string id] => runtime.Start(id),
            [IManagedGame game] => runtime.Start(game),
            _ => throw new ArgumentException("Expected an id or a managed game.")
        });
        IoCRegistrar.Register(RuntimeDependencies.StartRestored, args => args is [IManagedGame game] ? runtime.Start(game)
            : throw new ArgumentException("Expected a restored managed game."));
        IoCRegistrar.Register(MessageDependencies.Receiver, _ => new MessageReceiver(Ioc.Resolve<IMessageParser>(MessageDependencies.Parser), runtime));
        IoCRegistrar.Register(RuntimeDependencies.SnapshotAndStop, args => args is [ISnapshotStore store] ? runtime.SnapshotAndStop(store)
            : throw new ArgumentException("Expected a snapshot store."));
        IoCRegistrar.Register(RuntimeDependencies.Restore, args => args is [ISnapshotStore store] ? runtime.Restore(store)
            : throw new ArgumentException("Expected a snapshot store."));
        IoCRegistrar.Register(RuntimeDependencies.MemorySnapshots, _ => new MemorySnapshotStore(new ConcurrentDictionary<string, string>()));
        IoCRegistrar.Register(RuntimeDependencies.SnapshotAndStopCommand, args => args is [ISnapshotStore store]
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

    // Forwarders keep independently developed extensions source-compatible during migration.
    [Obsolete("Use IoCRegistrar.Register instead.")]
    internal static void Register(string key, Func<object[], object> strategy) => IoCRegistrar.Register(key, strategy);
    [Obsolete("Use ScopeExecution.InScope instead.")]
    internal static T InScope<T>(IGameScope scope, Func<T> action) => ScopeExecution.InScope(scope, action);

}
