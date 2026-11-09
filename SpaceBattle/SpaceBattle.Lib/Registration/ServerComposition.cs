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
            var output = log ?? new MemoryLog(new System.Collections.Concurrent.ConcurrentQueue<(string, Exception, ErrorAction, int)>());
            new RegisterIoCDependencyServerFoundation(options).Execute();
            new RegisterIoCDependencyServerRuntime(options).Execute();
            new RegisterIoCDependencyServerSecurity(options).Execute();
            configureCommands?.Invoke();
            new RegisterIoCDependencyServerPolicies(options, output).Execute();
            var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
            var runtime = Ioc.Resolve<GameRuntime>(RuntimeDependencies.Games);
            var validator = Ioc.Resolve<IPlayerTokenValidator>(AuthenticationDependencies.Validator);
            var parser = Ioc.Resolve<IMessageParser>(MessageDependencies.Parser);
            var whitelist = Ioc.Resolve<ICommandWhitelist>(MessagePipelineDependencies.Whitelist);
            return new ServerApplication(runtime, validator,
                player => new AuthenticatedMessageReceiver(parser, runtime, whitelist, player,
                    (message, identity) => ScopeExecution.InScope(scope,
                        () => Ioc.Resolve<IMessage>(AuthenticationDependencies.Authenticate, message, identity))),
                snapshots ?? new FileSnapshotStore(options.SnapshotPath, new object()), options, new object());
        }
        finally { Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", previous).Execute(); }
    }
}
