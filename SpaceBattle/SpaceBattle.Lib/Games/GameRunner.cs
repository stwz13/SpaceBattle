namespace SpaceBattle.Lib;

public class GameRunner : IGameRunner
{
    private readonly IGame _game;
    private readonly IServerThread _thread;
    private readonly Func<bool>? _canRun;
    private bool _scheduled;

    public GameRunner(IGame game, IServerThread thread, Func<bool>? canRun = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(thread);
        _game = game;
        _thread = thread;
        _canRun = canRun;
    }

    public void Post(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _game.Post(command);
        if (!_scheduled && CanRun())
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

    public void Wake()
    {
        if (!_scheduled && _game.HasCommands && CanRun())
        {
            _scheduled = true;
            _thread.Schedule(this);
        }
    }

    private bool CanRun() => _canRun?.Invoke() ?? true;

    private void Continue()
    {
        _scheduled = _game.HasCommands && CanRun();
        if (_scheduled)
        {
            _thread.Schedule(this);
        }
    }
}
