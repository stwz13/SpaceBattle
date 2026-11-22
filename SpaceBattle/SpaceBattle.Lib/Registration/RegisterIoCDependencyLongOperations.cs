namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyLongOperations : ICommand
{
    public void Execute()
    {
        var factory = new LongOperationFactory();
        IoCRegistrar.Register(LongOperationDependencies.Create, args => args is [ICommand step, ICommandReceiver receiver]
            ? factory.Create(step, receiver)
            : throw new ArgumentException("A long operation requires a step command and a command receiver."));
    }
}
