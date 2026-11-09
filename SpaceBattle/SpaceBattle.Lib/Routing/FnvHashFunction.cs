namespace SpaceBattle.Lib;

public class FnvHashFunction : IHashFunction
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;
    private const ulong FirstMultiplier = 0xFF51AFD7ED558CCDUL;
    private const ulong SecondMultiplier = 0xC4CEB9FE1A85EC53UL;

    public ulong Hash(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Finish(key.Aggregate(OffsetBasis, (hash, symbol) => Step(Step(hash, (byte)symbol), (byte)(symbol >> 8))));
    }

    private static ulong Step(ulong hash, byte value) => unchecked((hash ^ value) * Prime);

    private static ulong Finish(ulong hash) =>
        Spread(Multiply(Spread(Multiply(Spread(hash), FirstMultiplier)), SecondMultiplier));

    private static ulong Spread(ulong value) => value ^ (value >> 33);

    private static ulong Multiply(ulong value, ulong factor) => unchecked(value * factor);
}
