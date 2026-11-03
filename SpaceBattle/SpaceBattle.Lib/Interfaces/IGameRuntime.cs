namespace SpaceBattle.Lib;

public interface IGameCatalog : IGameRegistry
{
    IServer Server { get; }
    IReadOnlyCollection<IManagedGameHost> Games { get; }
}
public interface IGameLifecycle
{
    IManagedGameHost Start(string id);
    IManagedGameHost Start(IManagedGame game);
}
public interface IServerSnapshots
{
    Task<IReadOnlyDictionary<string, string>> SnapshotAndStop(ISnapshotStore store);
    Task Restore(ISnapshotStore store);
}
public partial interface IGameRuntime : IGameCatalog, IGameLifecycle, IServerSnapshots { }
