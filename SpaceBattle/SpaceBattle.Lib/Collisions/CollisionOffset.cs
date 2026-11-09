namespace SpaceBattle.Lib;

public static class CollisionOffset
{
    public const string PositionProperty = "Position";
    public const string VelocityProperty = "Velocity";

    public static Vector Between(IGameItem first, IGameItem second) => new Vector(
        Difference(first, second, PositionProperty).Concat(Difference(first, second, VelocityProperty)).ToArray());

    private static IEnumerable<int> Difference(IGameItem first, IGameItem second, string property) =>
        ((Vector)second.GetProperty(property)).Coordinates
            .Zip(((Vector)first.GetProperty(property)).Coordinates, (b, a) => b - a);
}
