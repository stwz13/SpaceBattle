using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyServerThread : ICommand
{
    private readonly int _inboxCapacity;

    public RegisterIoCDependencyServerThread(int inboxCapacity = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(inboxCapacity, 1);
        _inboxCapacity = inboxCapacity;
    }

    public void Execute()
    {
        Register(ServerThreadDependencies.Create, args =>
        {
            if (args.Length != 0)
            {
                throw new ArgumentException("A server thread takes no arguments.");
            }
            return new ServerThread(new BlockingCommandQueue(_inboxCapacity), new BlockingCommandQueue(), new QueueScheduler(),
                Ioc.Resolve<IExceptionHandler>(ServerThreadDependencies.ExceptionHandler),
                Ioc.Resolve<IExceptionFactory>("Factories.Exception"));
        });
        Register(ServerThreadDependencies.HardStop, args =>
            new HardStopCommand(ServerThreadOf(args), Ioc.Resolve<IExceptionFactory>("Factories.Exception")));
        Register(ServerThreadDependencies.SoftStop, args =>
            new SoftStopCommand(ServerThreadOf(args), Ioc.Resolve<IExceptionFactory>("Factories.Exception")));
    }

    private static IServerThread ServerThreadOf(object[] args)
    {
        if (args.Length != 1 || args[0] is not IServerThread thread)
        {
            throw new ArgumentException("A stop command requires the server thread it stops.");
        }
        return thread;
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
