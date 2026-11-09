namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyServerFoundation(ServerOptions options) : ICommand
{
    public void Execute()
    {
        new RegisterIoCDependencyExceptionFactory().Execute();
        new RegisterIoCDependencyBattleContextFactory().Execute();
        new RegisterIoCDependencyGameItemFactory().Execute();
        new RegisterIoCDependencyGameQuantum(TimeSpan.FromMilliseconds(options.QuantumMilliseconds)).Execute();
        new RegisterIoCDependencyGame().Execute();
        var handler = new DeferredExceptionHandler();
        IoCRegistrar.Register(ServerThreadDependencies.ExceptionHandler, _ => handler);
        if (options.PriorityQueues) new RegisterIoCDependencyPriorityThreads(options.InboxCapacity).Execute();
        else new RegisterIoCDependencyServerThread(options.InboxCapacity).Execute();
        new RegisterIoCDependencyRouting(options.RingReplicas).Execute();
        new RegisterIoCDependencyGameRunner().Execute();
        new RegisterIoCDependencyMessages().Execute();
        new RegisterIoCDependencyServer(options.ThreadCount).Execute();
    }
}
