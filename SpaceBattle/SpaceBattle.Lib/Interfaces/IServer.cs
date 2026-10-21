namespace SpaceBattle.Lib;

public interface IServer
{
    ServerState State { get; }
    int ThreadCount { get; }
    void Start();
    IServerThread AddThread();
    Task SoftStop();
    Task HardStop();
}
