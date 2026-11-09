using App;
namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyThreadErrorHandling(ILog log) : ICommand
{
    public void Execute()
    {
        var server = Ioc.Resolve<IServer>(ServerDependencies.Instance);
        var stop = new StopCurrentThreadErrorAction(Ioc.Resolve<IManagedGameRegistry>(RuntimeServiceDependencies.Registry), server, server);
        var actions = new Dictionary<ErrorAction, IErrorAction>
        {
            [ErrorAction.Retry] = new RetryErrorAction(),
            [ErrorAction.Skip] = new SkipErrorAction(),
            [ErrorAction.StopThread] = stop,
            [ErrorAction.StopGame] = stop
        };
        Ioc.Resolve<DeferredExceptionHandler>(ServerThreadDependencies.ExceptionHandler).Bind(
            new LoggingExceptionHandler(new ErrorRecovery("server",
                Ioc.Resolve<IErrorPolicy>(ErrorDependencies.Policy), log, actions)));
    }
}
