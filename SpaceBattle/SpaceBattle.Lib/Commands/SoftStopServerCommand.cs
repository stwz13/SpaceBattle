namespace SpaceBattle.Lib;

public class SoftStopServerCommand : ICommand
{
    private readonly IServer _server;

    public SoftStopServerCommand(IServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        _server = server;
    }

    public void Execute() => _ = _server.SoftStop();
}
