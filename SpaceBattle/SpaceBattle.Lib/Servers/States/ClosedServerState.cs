namespace SpaceBattle.Lib;

internal sealed class ClosedServerState : ServerStateBase
{
    public static readonly ClosedServerState Instance = new();

    public override bool IsRunning => false;

    public override void Start(IServerContext server) =>
        throw new InvalidOperationException("The server has already been started.");

    public override IServerThread AddThread(IServerContext server) =>
        throw new InvalidOperationException("The server is stopping.");

    public override Task Stop(IServerContext server, Action<IServerThread> send) =>
        throw new InvalidOperationException("The server has already been stopped.");
}
