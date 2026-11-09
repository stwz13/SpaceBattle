namespace SpaceBattle.Lib;

internal abstract class OpenServerState : ServerStateBase
{
    public override Task Stop(IServerContext server, Action<IServerThread> send)
    {
        server.Become(ClosedServerState.Instance);
        return server.Shutdown(send);
    }
}
