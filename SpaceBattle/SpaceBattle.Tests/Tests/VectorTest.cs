using SpaceBattle.Lib;


public class VectorTest
{
    [Fact]
    public void Changing_The_Input_Array_Does_Not_Change_The_Vector()
    {
        int[] coordinates = [1, 2];
        var vector = SpaceBattle.Tests.TestValues.Vector(coordinates);
        var hash = vector.GetHashCode();
        coordinates[0] = 99;
        Assert.Equal(SpaceBattle.Tests.TestValues.Vector([1, 2]), vector);
        Assert.Equal(hash, vector.GetHashCode());
    }

    [Fact]
    public void TestCreateVector_Positive()
    {
        var coords = new int[] { 1, 2, 3 };

        var vec = SpaceBattle.Tests.TestValues.Vector(coords);

        Assert.NotNull(vec);
        Assert.Equal(3, vec.Dim);
    }

    [Fact]
    public void TestAdditionCorrectCoordinatesReternsSum()
    {
        var vec1 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, -1, 2 });
        var vec2 = SpaceBattle.Tests.TestValues.Vector(new int[] { -1, 1, -2 });
        var expected = SpaceBattle.Tests.TestValues.Vector(new int[] { 0, 0, 0 });

        var sumVec = vec1 + vec2;

        Assert.Equal(expected, sumVec);
    }

    [Fact]
    public void TestAdditionalVectorsFromDiffrentDimensionsThrowsArgumentException()
    {
        var vec1 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });
        var vec2 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2 });

        Assert.Throws<ArgumentException>(() => vec1 + vec2);
        Assert.Throws<ArgumentException>(() => vec2 + vec1);
    }

    [Fact]
    public void TestVectorsWithSameCoordinatesReturnsTrueWithEqualMethod()
    {
        var vec1 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });
        var vec2 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });

        Assert.True(vec1.Equals(vec2));
        Assert.True(vec2.Equals(vec1));
    }

    [Fact]
    public void TestVectorsWithSameCoordinatesReturnsTrueWithEqualOperator()
    {
        var vec1 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });
        var vec2 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });

        Assert.True(vec1 == vec2);

    }

    [Fact]
    public void TestVectorsWithDiffrenteCoordinatesReturnsFalseWithEqualMethod()
    {
        var vec1 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });
        var vec2 = SpaceBattle.Tests.TestValues.Vector(new int[] { 4, 2, 3 });

        Assert.False(vec1.Equals(vec2));
        Assert.False(vec2.Equals(vec1));

    }

    [Fact]
    public void TestVectorAndArrayAreNotEqual()
    {
        var vec1 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });
        var vec2 = new int[] { 1, 2, 3 };

        Assert.False(vec1.Equals(vec2));
    }

    [Fact]
    public void TestVectorsWithDiffrenteCoordinatesReturnsTrueWithNotEqualOperator()
    {
        var vec1 = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });
        var vec2 = SpaceBattle.Tests.TestValues.Vector(new int[] { 4, 2, 3 });

        Assert.True(vec1 != vec2);

    }

    [Fact]
    public void TestSameVectorsHaveSameHashCode()
    {
        var vec = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });

        var duplicateVec = SpaceBattle.Tests.TestValues.Vector(new int[] { 1, 2, 3 });

        Assert.Equal(vec.GetHashCode(), duplicateVec.GetHashCode());
    }

    [Fact]
    public void TestDiffrentVectorsHaveDiffrentHash()
    {
        var vec1 = SpaceBattle.Tests.TestValues.Vector(new int[] { 3, 4, 5 });

        var vec2 = SpaceBattle.Tests.TestValues.Vector(new int[] { 10, 11, 12 });

        Assert.NotEqual(vec1.GetHashCode(), vec2.GetHashCode());
    }

}
