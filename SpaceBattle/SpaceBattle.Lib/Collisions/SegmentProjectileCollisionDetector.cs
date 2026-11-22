namespace SpaceBattle.Lib;

public sealed class SegmentProjectileCollisionDetector : IProjectileCollisionDetector
{
    public IGameItem? FirstHit(MovementSegment projectile, IEnumerable<(IGameItem Item, MovementSegment Path)> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        IGameItem? first = null;
        long firstNumerator = 1;
        long firstDenominator = 1;
        foreach (var (item, path) in targets)
        {
            if (!Contact(projectile, path, out var numerator, out var denominator))
                continue;

            if (first is not null && (Int128)numerator * firstDenominator >= (Int128)firstNumerator * denominator)
                continue;

            first = item;
            firstNumerator = numerator;
            firstDenominator = denominator;
        }

        return first;
    }

    private static bool Contact(MovementSegment projectile, MovementSegment target,
        out long numerator, out long denominator)
    {
        var x = (long)target.StartX - projectile.StartX;
        var y = (long)target.StartY - projectile.StartY;
        var dx = projectile.DeltaX - target.DeltaX;
        var dy = projectile.DeltaY - target.DeltaY;
        if (dx == 0 && dy == 0)
        {
            numerator = 0;
            denominator = 1;
            return x == 0 && y == 0;
        }

        numerator = dx != 0 ? x : y;
        denominator = dx != 0 ? dx : dy;
        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        return numerator >= 0 && numerator <= denominator && (Int128)x * dy == (Int128)y * dx;
    }
}
