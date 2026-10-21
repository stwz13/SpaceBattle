namespace SpaceBattle.Lib;

internal sealed class CreatedServerState : OpenServerState
{
    public static readonly CreatedServerState Instance = new();

    public override bool IsRunning => false;

    public override void Start(IServerContext server)
    {
        server.Become(RunningServerState.Instance);
        server.SpawnInitialThreads();
        server.RunThreads();
    }

    public override IServerThread AddThread(IServerContext server) => server.Spawn();
}
