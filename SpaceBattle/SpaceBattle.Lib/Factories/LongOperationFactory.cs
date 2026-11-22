namespace SpaceBattle.Lib;

public sealed class LongOperationFactory
{
    public LongOperationCommands Create(ICommand step, ICommandReceiver receiver)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(receiver);

        var empty = new EmptyCommand();
        var state = new LongOperationState(empty);
        return new LongOperationCommands(new StartCommand(() => state.Start(CreateRun)), new StopCommand(state.Stop));

        (CommandInjectableCommand Continuation, ICommand Send) CreateRun()
        {
            var continuation = new CommandInjectableCommand(empty);
            var send = new SendCommand(continuation, receiver);
            continuation.Inject(new RepeatOperationCommand(step, send, () => state.IsCurrent(continuation),
                () => state.StopIfCurrent(continuation)));
            return (continuation, send);
        }
    }
}
