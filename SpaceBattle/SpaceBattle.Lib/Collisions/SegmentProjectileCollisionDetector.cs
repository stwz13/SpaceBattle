namespace SpaceBattle.Lib;

public sealed class SegmentProjectileCollisionDetector : IProjectileCollisionDetector
{
    public IGameItem? FirstHit(Vector start, Vector end, IEnumerable<IGameItem> targets)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(targets);
        if (start.Dim != 2 || end.Dim != 2)
            throw new ArgumentException("The collision detector expects a two-dimensional segment.");
        var dx = (Int128)end.Coordinates[0] - start.Coordinates[0];
        var dy = (Int128)end.Coordinates[1] - start.Coordinates[1];
        var length = dx * dx + dy * dy;
        return targets.Select(item => (Item: item, Position: BattleItemProperties.Read<Vector>(item, "Position")))
            .Where(entry => entry.Position?.Dim == 2)
            .Select(entry =>
            {
                var x = (Int128)entry.Position!.Coordinates[0] - start.Coordinates[0];
                var y = (Int128)entry.Position.Coordinates[1] - start.Coordinates[1];
                var along = x * dx + y * dy;
                var hit = length == 0 ? x == 0 && y == 0 : x * dy == y * dx && along >= 0 && along <= length;
                return (entry.Item, Along: along, Hit: hit);
            })
            .Where(entry => entry.Hit).OrderBy(entry => entry.Along).Select(entry => entry.Item).FirstOrDefault();
    }
}
