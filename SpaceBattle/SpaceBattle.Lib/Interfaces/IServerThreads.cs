namespace SpaceBattle.Lib;

public interface IServerThreads
{
    int ThreadCount { get; }
    IServerThread AddThread();
}
