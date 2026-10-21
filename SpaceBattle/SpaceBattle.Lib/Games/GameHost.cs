namespace SpaceBattle.Lib;

public class GameHost : IGameHost
{
    private readonly IGameRunner _runner;
    private readonly IServerThread _thread;
    private readonly IMessageInterpreter _interpreter;

    public GameHost(IGameRunner runner, IServerThread thread, IMessageInterpreter interpreter)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(interpreter);
        _runner = runner;
        _thread = thread;
        _interpreter = interpreter;
    }

    public void Send(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_thread.TryAdd(new PostToGameCommand(_runner, new InterpretMessageCommand(_interpreter, message))))
        {
            throw new ServerOverloadedException("The server thread of the game cannot accept more messages.");
        }
    }
}
