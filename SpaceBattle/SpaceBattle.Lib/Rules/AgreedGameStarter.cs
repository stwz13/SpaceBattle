namespace SpaceBattle.Lib;

public sealed class AgreedGameStarter
{
    private readonly Func<string, GameActionRules, IManagedGame> _create;
    private readonly IGameLifecycle _lifecycle;

    public AgreedGameStarter(Func<string, GameActionRules, IManagedGame> create, IGameLifecycle lifecycle)
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(lifecycle);
        _create = create;
        _lifecycle = lifecycle;
    }

    public IManagedGameHost Start(IConfirmedGameStart lobby, string playerId, long revision)
    {
        ArgumentNullException.ThrowIfNull(lobby);
        return lobby.Start(playerId, revision, rules => _lifecycle.Start(_create(lobby.GameId, rules)));
    }
}
