namespace SpaceBattle.Lib;

public partial interface IManagedGameHost : IGameHost
{
    string Id { get; }
    IServerThread Thread { get; }
    GameHostState State { get; }
    IManagedGame Game { get; }
    IGameRunner Runner { get; }
    Task Execute(Action action);
    Task Enqueue(Action action);
    Task Wake();
    Task<string> PauseAndSnapshot();
    Task Resume();
    void MarkStopped();
}
