namespace SpaceBattle.Lib;

public class PostToGameCommand : ICommand
{
    private readonly IGameRunner _runner;
    private readonly ICommand _command;

    public PostToGameCommand(IGameRunner runner, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(command);
        _runner = runner;
        _command = command;
    }

    public void Execute() => _runner.Post(_command);
}
