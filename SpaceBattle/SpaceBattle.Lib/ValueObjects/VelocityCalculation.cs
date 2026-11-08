namespace SpaceBattle.Lib;

internal static class VelocityCalculation
{
    public static Vector FromSpeedAndDirection(int speed, Angle direction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(speed);
        return new Vector(
            RoundCoordinate(speed * Math.Cos(direction.Radians)),
            RoundCoordinate(speed * Math.Sin(direction.Radians)));
    }

    private static int RoundCoordinate(double value) =>
        checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
}
