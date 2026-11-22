namespace SpaceBattle.Lib;

// Its state is restored by the feature that owns the operation.
internal sealed class TransientGameCommand(ICommand command) : ICommand
{
    public void Execute() => command.Execute();
}
