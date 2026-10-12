namespace SpaceBattle.Tests;

using SpaceBattle.Lib;
public class AngleTest
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(8, 0)]
    [InlineData(15, 7)]
    [InlineData(-1, 7)]
    [InlineData(-8, 0)]
    [InlineData(-9, 7)]
    [InlineData(int.MinValue, 0)]
    [InlineData(int.MaxValue, 7)]
    public void Constructor_Normalizes_Positive_And_Negative_Turns(int numerator, int expected)
    {
        var angle = new Angle(numerator);

        Assert.Equal(expected, angle.Numerator);
        Assert.Equal(2 * Math.PI * expected / Angle.Denominator, angle.Radians);
    }

    [Fact]
    public void TestAngleCreationReturnsCorrectAngle()
    {
        var a = new Angle(5);

        Assert.Equal(5, a.Numerator);
        Assert.Equal(8, Angle.Denominator);
    }

    [Fact]
    public void TestAnglesAdditionReturnsCorrectAngle()
    {
        var a = new Angle(5);
        var b = new Angle(7);

        var c = new Angle(4);

        Assert.Equal(c, a + b);
    }

    [Fact]
    public void TestAnglesWithIntDifferenceOfTurnsAreEqualWithEqualsMethod()
    {
        var a = new Angle(15);
        var b = new Angle(23);

        Assert.True(a.Equals(b));
        Assert.True(b.Equals(a));
    }

    [Fact]
    public void TestAnglesWithIntDifferenceOfTurnsAreEqualWithOverloadedEqualsMethod()
    {
        var a = new Angle(15);
        var b = new Angle(23);

        Assert.True(a == b);
        Assert.True(b == a);
    }

    [Fact]
    public void TestDifferentAnglesAreNotEqualWithEqualsMethod()
    {
        var a = new Angle(1);
        var b = new Angle(2);

        Assert.False(a.Equals(b));
        Assert.False(b.Equals(a));
    }

    [Fact]
    public void TestAngleAndNotAngleAreNotEqual()
    {
        var a = new Angle(5);
        var b = 5 / Angle.Denominator;

        Assert.False(a.Equals(b));
        Assert.False(b.Equals(a));
    }

    [Fact]
    public void TestDifferentAnglesAreNotEqualWithOverloadedEqualsMethod()
    {
        var a = new Angle(1);
        var b = new Angle(2);

        Assert.True(a != b);
        Assert.True(b != a);
    }

    [Fact]
    public void TestSameAnglesHaveSameHashCode()
    {
        var a = new Angle(15);
        var b = new Angle(23);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void TestAngleSinСalculatedCorrectly()
    {
        var a = new Angle(5);

        Assert.Equal(-Math.Sqrt(2) / 2, Math.Sin(a.Radians), 10);
    }

    [Fact]
    public void TestAngleCosСalculatedCorrectly()
    {
        var a = new Angle(5);

        Assert.Equal(-Math.Sqrt(2) / 2, Math.Cos(a.Radians), 10);
    }
}
