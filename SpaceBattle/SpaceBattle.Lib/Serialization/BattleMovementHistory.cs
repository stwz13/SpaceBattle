namespace SpaceBattle.Lib;

internal sealed class BattleMovementHistory(
    IGameItemStorage storage, WorldMovementSegments segments, IReadOnlyList<SavedMovement> restored)
{
    public const string Dependency = "Battle.MovementHistory";
    private IReadOnlyList<SavedMovement> _restored = restored;

    public IReadOnlyList<SavedMovement> Save() => _restored.Concat(
        segments.Pending.Select(entry => SavedMovement.From(entry.Key, entry.Value))).ToArray();

    public void Restore()
    {
        if (_restored.Count == 0)
            return;

        var items = storage.GetSnapshot().ToDictionary(
            item => (string)item.GetProperty(SerializationDependencies.IdProperty), StringComparer.Ordinal);
        foreach (var path in _restored)
        {
            if (items.TryGetValue(path.ItemId, out var item))
                segments.Record(item, new Vector(path.StartX, path.StartY), new Vector(path.EndX, path.EndY));
        }

        _restored = [];
    }
}

internal sealed record SavedMovement(string ItemId, int StartX, int StartY, int EndX, int EndY)
{
    public static SavedMovement From(IGameItem item, MovementSegment path) =>
        new((string)item.GetProperty(SerializationDependencies.IdProperty),
            path.StartX, path.StartY, path.EndX, path.EndY);
}
