namespace SpaceBattle.Lib;

internal interface IServerContext
{
    void Become(IServerState state);

    void SpawnInitialThreads();

    void RunThreads();

    IServerThread Spawn();

    IServerThread SpawnAndRun();

    Task Shutdown(Action<IServerThread> send);
}
