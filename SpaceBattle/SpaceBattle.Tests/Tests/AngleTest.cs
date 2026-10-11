namespace SpaceBattle.Tests;

using SpaceBattle.Lib;
public class AngleTest
{
    [Fact]
    public void TestAngleCreationReturnsCorrectAngle()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(5);

        Assert.Equal(5, a.Numerator);
        Assert.Equal(8, Angle.Denominator);
    }

    [Fact]
    public void TestAnglesAdditionReturnsCorrectAngle()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(5);
        var b = SpaceBattle.Tests.TestValues.Angle(7);

        var c = SpaceBattle.Tests.TestValues.Angle(4);

        Assert.Equal(c, a + b);
    }

    [Fact]
    public void TestAnglesWithIntDifferenceOfTurnsAreEqualWithEqualsMethod()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(15);
        var b = SpaceBattle.Tests.TestValues.Angle(23);

        Assert.True(a.Equals(b));
        Assert.True(b.Equals(a));
    }

    [Fact]
    public void TestAnglesWithIntDifferenceOfTurnsAreEqualWithOverloadedEqualsMethod()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(15);
        var b = SpaceBattle.Tests.TestValues.Angle(23);

        Assert.True(a == b);
        Assert.True(b == a);
    }

    [Fact]
    public void TestDifferentAnglesAreNotEqualWithEqualsMethod()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(1);
        var b = SpaceBattle.Tests.TestValues.Angle(2);

        Assert.False(a.Equals(b));
        Assert.False(b.Equals(a));
    }

    [Fact]
    public void TestAngleAndNotAngleAreNotEqual()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(5);
        var b = 5 / Angle.Denominator;

        Assert.False(a.Equals(b));
        Assert.False(b.Equals(a));
    }

    [Fact]
    public void TestDifferentAnglesAreNotEqualWithOverloadedEqualsMethod()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(1);
        var b = SpaceBattle.Tests.TestValues.Angle(2);

        Assert.True(a != b);
        Assert.True(b != a);
    }

    [Fact]
    public void TestSameAnglesHaveSameHashCode()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(15);
        var b = SpaceBattle.Tests.TestValues.Angle(23);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void TestAngleSinСalculatedCorrectly()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(5);

        Assert.Equal(-Math.Sqrt(2) / 2, Math.Sin(a.Radians), 10);
    }

    [Fact]
    public void TestAngleCosСalculatedCorrectly()
    {
        var a = SpaceBattle.Tests.TestValues.Angle(5);

        Assert.Equal(-Math.Sqrt(2) / 2, Math.Cos(a.Radians), 10);
    }
}
