namespace SpaceBattle.Lib;

public sealed partial class GameRuntime
{
    internal IGameRebalanceService Rebalancing { get; set; } = null!;
    internal Func<string, IServerThread> Route { get; set; } = null!;
    public Task<IServerThread> AddThread() => Rebalancing.AddThread();
    public Task Rebalance() => Rebalancing.Rebalance();
}
