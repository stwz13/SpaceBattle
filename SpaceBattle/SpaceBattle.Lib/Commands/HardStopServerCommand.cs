namespace SpaceBattle.Lib;

public class HardStopServerCommand : ICommand
{
    private readonly IServer _server;

    public HardStopServerCommand(IServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        _server = server;
    }

    public void Execute() => _ = _server.HardStop();
}
