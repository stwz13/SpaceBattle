namespace SpaceBattle.Lib;

internal interface IServerState
{
    bool IsRunning { get; }

    void EnsureRunning();

    void Start(IServerContext server);

    IServerThread AddThread(IServerContext server);

    Task Stop(IServerContext server, Action<IServerThread> send);
}
