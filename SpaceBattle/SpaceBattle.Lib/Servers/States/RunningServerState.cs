namespace SpaceBattle.Lib;

internal sealed class RunningServerState : OpenServerState
{
    public static readonly RunningServerState Instance = new();

    public override bool IsRunning => true;

    public override void Start(IServerContext server) =>
        throw new InvalidOperationException("The server has already been started.");

    public override IServerThread AddThread(IServerContext server) => server.SpawnAndRun();
}
