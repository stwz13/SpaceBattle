using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

internal static class TestValues
{
    public static Vector Vector(int[] coordinates) => new VectorFactory().Create(coordinates);
    public static Angle Angle(int numerator) => new AngleFactory().Create(numerator);
}
