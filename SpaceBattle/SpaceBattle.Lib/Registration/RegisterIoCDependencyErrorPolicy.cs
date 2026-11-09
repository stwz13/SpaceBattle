using System.Collections.Concurrent;
using App;
namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyErrorPolicy(
    IReadOnlyDictionary<Type, (ErrorAction Action, int Retries)> rules, ILog? log = null) : ICommand
{
    public void Execute()
    {
        if (rules.Any(rule => rule.Value.Retries < 0 || !typeof(Exception).IsAssignableFrom(rule.Key)))
            throw new ArgumentException("Error rules require exception types and nonnegative retry limits.");
        var policy = new ExceptionErrorPolicy(rules.ToDictionary());
        var output = log ?? new MemoryLog(new ConcurrentQueue<(string, Exception, ErrorAction, int)>());
        var runtime = Ioc.Resolve<IGameRuntime>(RuntimeDependencies.Games);
        var games = Ioc.Resolve<IManagedGameRegistry>(RuntimeServiceDependencies.Registry);
        var actions = new Dictionary<ErrorAction, IErrorAction>
        {
            [ErrorAction.Retry] = new RetryErrorAction(),
            [ErrorAction.Skip] = new SkipErrorAction(),
            [ErrorAction.StopGame] = new StopGameErrorAction(games),
            [ErrorAction.StopThread] = new StopThreadErrorAction(games, runtime.Server)
        };
        IoCRegistrar.Register(ErrorDependencies.Policy, _ => policy);
        IoCRegistrar.Register(ErrorDependencies.Log, _ => output);
        Ioc.Resolve<IMessageInterpreter>(MessagePipelineDependencies.Decorate,
            (Func<IMessageInterpreter, IMessageInterpreter>)(inner => new PolicyMessageInterpreter(inner,
                (message, action) => new ErrorPolicyCommand(action, new ErrorRecovery(message.GameId, policy, output, actions)))));
    }
}
