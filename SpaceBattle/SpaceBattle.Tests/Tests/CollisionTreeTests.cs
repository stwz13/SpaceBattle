using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CollisionTreeTests
{
    private static CollisionTree Tree(params int[][] vectors) => new CollisionTree(vectors);

    [Fact]
    public void Contains_Finds_Every_Stored_Vector()
    {
        var tree = Tree([1, 2, 3, 4], [1, 2, 3, 5], [1, 2, 9, 4], [1, 7, 3, 4], [-5, 0, 0, 0]);

        Assert.True(tree.Contains([1, 2, 3, 4]));
        Assert.True(tree.Contains([1, 2, 3, 5]));
        Assert.True(tree.Contains([1, 2, 9, 4]));
        Assert.True(tree.Contains([1, 7, 3, 4]));
        Assert.True(tree.Contains([-5, 0, 0, 0]));
    }

    [Theory]
    [InlineData(1, 2, 3, 6)]
    [InlineData(1, 2, 8, 4)]
    [InlineData(1, 8, 3, 4)]
    [InlineData(2, 2, 3, 4)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(4, 3, 2, 1)]
    public void Contains_Does_Not_Find_Missing_Vectors(int dx, int dy, int dvx, int dvy)
    {
        var tree = Tree([1, 2, 3, 4], [1, 2, 3, 5], [1, 2, 9, 4], [1, 7, 3, 4], [-5, 0, 0, 0]);

        Assert.False(tree.Contains([dx, dy, dvx, dvy]));
    }

    [Fact]
    public void Contains_Does_Not_Accept_A_Prefix_Or_An_Extension_Of_A_Stored_Vector()
    {
        var tree = Tree([1, 2, 3, 4]);

        Assert.False(tree.Contains([1, 2, 3]));
        Assert.False(tree.Contains([1]));
        Assert.False(tree.Contains([]));
        Assert.False(tree.Contains([1, 2, 3, 4, 5]));
    }

    [Fact]
    public void Empty_Tree_Contains_Nothing()
    {
        var tree = Tree();

        Assert.False(tree.Contains([1, 2, 3, 4]));
        Assert.False(tree.Contains([]));
    }

    [Fact]
    public void Order_Of_Coordinates_Matters()
    {
        var tree = Tree([1, 2, 3, 4]);

        Assert.False(tree.Contains([4, 3, 2, 1]));
        Assert.False(tree.Contains([2, 1, 3, 4]));
    }

    [Fact]
    public void Duplicates_Are_Harmless()
    {
        var tree = Tree([1, 2, 3, 4], [1, 2, 3, 4]);

        Assert.True(tree.Contains([1, 2, 3, 4]));
    }

    [Fact]
    public void Tree_Is_Built_From_A_Lazy_Sequence_Of_Paths_Once()
    {
        var enumerations = 0;
        IEnumerable<IEnumerable<int>> Paths()
        {
            enumerations++;
            yield return new[] { 1, 2 };
            yield return new[] { 1, 3 };
        }

        var tree = new CollisionTree(Paths());

        Assert.True(tree.Contains([1, 2]));
        Assert.True(tree.Contains([1, 3]));
        Assert.False(tree.Contains([1, 4]));
        Assert.Equal(1, enumerations);
    }

    [Fact]
    public void Large_Trees_Are_Supported()
    {
        var vectors = Enumerable.Range(-20, 41)
            .SelectMany(dx => Enumerable.Range(-20, 41), (dx, dy) => new[] { dx, dy, dx - dy, dx + dy })
            .ToArray();

        var tree = new CollisionTree(vectors);

        Assert.True(tree.Contains([7, -3, 10, 4]));
        Assert.False(tree.Contains([7, -3, 10, 5]));
        Assert.False(tree.Contains([21, 0, 21, 21]));
    }
}
