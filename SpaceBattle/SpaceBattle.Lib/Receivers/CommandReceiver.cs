namespace SpaceBattle.Lib;

public sealed class CommandReceiver : ICommandReceiver
{
    private readonly Action<ICommand> _post;

    public CommandReceiver(Action<ICommand> post)
    {
        ArgumentNullException.ThrowIfNull(post);
        _post = post;
    }

    public void Receive(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _post(command);
    }
}
