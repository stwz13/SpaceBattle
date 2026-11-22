namespace SpaceBattle.Lib;

public sealed class BattleShipKindClassifier : IBattleShipClassifier
{
    private readonly IReadOnlySet<string> _kinds;

    public BattleShipKindClassifier(IEnumerable<string> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        _kinds = kinds.ToHashSet(StringComparer.Ordinal);
    }

    public bool IsShip(IGameItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return BattleItemProperties.Read<string>(item, SerializationDependencies.KindProperty) is string kind &&
            _kinds.Contains(kind);
    }
}
