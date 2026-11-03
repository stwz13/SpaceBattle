using App;
using App.Scopes;

namespace SpaceBattle.Lib;

/// <summary>The single composition root for a running server. No runtime command resolves these services itself.</summary>
public static class ServerComposition
{
    public static ServerApplication Create(ServerOptions options, ISnapshotStore? snapshots = null,
        Action? configureCommands = null, ILog? log = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ThreadCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.InboxCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.RingReplicas, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(options.QuantumMilliseconds);
        new InitCommand().Execute();
        var previous = Ioc.Resolve<object>("IoC.Scope.Current");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", Ioc.Resolve<object>("IoC.Scope.Create")).Execute();
        try
        {
            new RegisterIoCDependencyExceptionFactory().Execute();
            new RegisterIoCDependencyBattleContextFactory().Execute();
            new RegisterIoCDependencyGameItemFactory().Execute();
            new RegisterIoCDependencyGameQuantum(TimeSpan.FromMilliseconds(options.QuantumMilliseconds)).Execute();
            new RegisterIoCDependencyGame().Execute();
            var output = log ?? new MemoryLog(new System.Collections.Concurrent.ConcurrentQueue<(string, Exception, ErrorAction, int)>());
            RegisterIoCDependencyManagedGames.Register(ServerThreadDependencies.ExceptionHandler,
                _ => new LoggingExceptionHandler(output));
            if (options.PriorityQueues) new RegisterIoCDependencyPriorityThreads(options.InboxCapacity).Execute();
            else new RegisterIoCDependencyServerThread(options.InboxCapacity).Execute();
            new RegisterIoCDependencyRouting(options.RingReplicas).Execute();
            new RegisterIoCDependencyGameRunner().Execute();
            new RegisterIoCDependencyMessages().Execute();
            new RegisterIoCDependencyServer(options.ThreadCount).Execute();
            new RegisterIoCDependencyGameSnapshot().Execute();
            new RegisterIoCDependencyGameItems().Execute();
            options.ItemKinds.ToList().ForEach(kind => new RegisterIoCDependencyItemKind(kind.Key, kind.Value).Execute());
            new RegisterIoCDependencyManagedGames().Execute();
            new RegisterIoCDependencyGameTransfer(options.InboxCapacity).Execute();
            new RegisterIoCDependencyRebalancing().Execute();
            new RegisterIoCDependencyThreadRemoval().Execute();
            new RegisterIoCDependencyJwtAuthentication(options.JwtIssuer, options.JwtAudience, options.JwtSecret).Execute();
            new RegisterIoCDependencyCommandWhitelist(options.AllowedCommands).Execute();
            configureCommands?.Invoke();
            new RegisterIoCDependencyPlayerAuthorization(options.CommandTargets, options.Players).Execute();
            new RegisterIoCDependencyErrorPolicy(new Dictionary<Type, (ErrorAction, int)>(), output).Execute();
            var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
            var runtime = Ioc.Resolve<GameRuntime>("Server.Games");
            var validator = Ioc.Resolve<IPlayerTokenValidator>("Auth.Validator");
            var parser = Ioc.Resolve<IMessageParser>(MessageDependencies.Parser);
            var whitelist = Ioc.Resolve<ICommandWhitelist>("Messages.Whitelist");
            return new ServerApplication(runtime, validator,
                player => new AuthenticatedMessageReceiver(parser, runtime, whitelist, player,
                    (message, identity) => RegisterIoCDependencyManagedGames.InScope(scope,
                        () => Ioc.Resolve<IMessage>("Messages.Authenticate", message, identity))),
                snapshots ?? new FileSnapshotStore(options.SnapshotPath, new object()), options, new object());
        }
        finally { Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", previous).Execute(); }
    }
}
