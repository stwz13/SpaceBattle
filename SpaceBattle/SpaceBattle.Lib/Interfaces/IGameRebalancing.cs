namespace SpaceBattle.Lib;

public interface IGameRebalancing
{
    Task<IServerThread> AddThread();
    Task Rebalance();
}
public partial interface IGameRuntime : IGameRebalancing { }
public partial interface IManagedGameHost { void Relocate(IManagedGameHost replacement); }
