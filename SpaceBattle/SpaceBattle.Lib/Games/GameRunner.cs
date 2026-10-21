namespace SpaceBattle.Lib;

public class GameRunner : IGameRunner
{
    private readonly IGame _game;
    private readonly IServerThread _thread;
    private bool _scheduled;

    public GameRunner(IGame game, IServerThread thread)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(thread);
        _game = game;
        _thread = thread;
    }

    public void Post(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _game.Post(command);
        if (!_scheduled)
        {
            _scheduled = true;
            _thread.Schedule(this);
        }
    }

    public void Execute()
    {
        try
        {
            _game.Execute();
        }
        finally
        {
            Continue();
        }
    }

    private void Continue()
    {
        _scheduled = _game.HasCommands;
        if (_scheduled)
        {
            _thread.Schedule(this);
        }
    }
}
