using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyServer : ICommand
{
    private readonly int _threadCount;

    public RegisterIoCDependencyServer(int threadCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threadCount, 1);
        _threadCount = threadCount;
    }

    public void Execute()
    {
        var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Current"));
        var server = new Server(
            Ioc.Resolve<IConsistentHashRing<IServerThread>>(RoutingDependencies.Ring),
            () => ScopeExecution.InScope(scope, () => Ioc.Resolve<IServerThread>(ServerThreadDependencies.Create)),
            thread => ScopeExecution.InScope(scope, () => Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.SoftStop, thread)),
            thread => ScopeExecution.InScope(scope, () => Ioc.Resolve<Lib.ICommand>(ServerThreadDependencies.HardStop, thread)),
            _threadCount);
        Register(ServerDependencies.Instance, args => Checked(args, server));
        Register(ServerDependencies.Start, args => Checked(args, new StartServerCommand(server)));
        Register(ServerDependencies.SoftStop, args => Checked(args, new SoftStopServerCommand(server)));
        Register(ServerDependencies.HardStop, args => Checked(args, new HardStopServerCommand(server)));
    }


    private static object Checked(object[] args, object result) =>
        args.Length == 0 ? result : throw new ArgumentException("Server dependencies take no arguments.");

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
