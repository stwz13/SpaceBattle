namespace SpaceBattle.Lib;

internal sealed class CommandInjectableCommand : ICommand
{
    private ICommand _command;

    public CommandInjectableCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _command = command;
    }

    public void Inject(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _command = command;
    }

    public void Execute() => _command.Execute();
}
