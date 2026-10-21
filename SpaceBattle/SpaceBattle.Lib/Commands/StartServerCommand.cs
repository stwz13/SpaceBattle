namespace SpaceBattle.Lib;

public class StartServerCommand : ICommand
{
    private readonly IServer _server;

    public StartServerCommand(IServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        _server = server;
    }

    public void Execute() => _server.Start();
}
