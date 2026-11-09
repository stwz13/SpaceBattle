namespace SpaceBattle.Lib;

public interface IServerThreadCatalog
{
    IReadOnlyList<(string Id, IServerThread Thread, Task? Completion)> Threads { get; }
}
