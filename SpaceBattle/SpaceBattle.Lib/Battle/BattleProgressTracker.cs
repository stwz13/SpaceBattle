namespace SpaceBattle.Lib;

internal sealed class BattleProgressTracker : IBattleProgressTracker
{
    private readonly IReadOnlyList<string> _players;
    public BattleProgress Current { get; private set; }

    public BattleProgressTracker(IReadOnlyList<string> players, BattleProgress? restored)
    {
        ArgumentNullException.ThrowIfNull(players);
        _players = players;
        Current = restored ?? new BattleProgress(false, false, null);
    }

    public void Observe(IEnumerable<IGameItem> ships)
    {
        if (Current.IsFinished) return;
        var active = ships.Where(ship => !BattleItemProperties.IsDestroyed(ship))
            .Select(ship => BattleItemProperties.Read<string>(ship, "Owner"))
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        var surviving = _players.Where(active.Contains).ToArray();
        if (surviving.Length == _players.Count && !Current.HasStarted)
            Current = new BattleProgress(true, false, null);
        else if (Current.HasStarted && surviving.Length < _players.Count)
            Current = new BattleProgress(true, true, surviving.SingleOrDefault());
    }
}
