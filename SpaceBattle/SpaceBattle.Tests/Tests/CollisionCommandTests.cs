using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CollisionCommandTests
{
    private sealed class CountingCommand : Lib.ICommand
    {
        public int Count { get; private set; }
        public void Execute() => Count++;
    }

    private static CollisionTree Tree() => new CollisionTree(new[] { new[] { 5, 0, -1, 0 }, new[] { 4, 0, -1, 0 } });

    [Theory]
    [InlineData(5, 0, -1, 0)]
    [InlineData(4, 0, -1, 0)]
    public void Command_Runs_The_Consequence_When_The_Offset_Is_In_The_Tree(int dx, int dy, int dvx, int dvy)
    {
        var consequence = new CountingCommand();

        new CollisionCommand(Tree(), () => new Vector(dx, dy, dvx, dvy), consequence).Execute();

        Assert.Equal(1, consequence.Count);
    }

    [Theory]
    [InlineData(6, 0, -1, 0)]
    [InlineData(5, 0, 0, 0)]
    [InlineData(0, 0, 0, 0)]
    public void Command_Does_Nothing_When_The_Offset_Is_Not_In_The_Tree(int dx, int dy, int dvx, int dvy)
    {
        var consequence = new CountingCommand();

        new CollisionCommand(Tree(), () => new Vector(dx, dy, dvx, dvy), consequence).Execute();

        Assert.Equal(0, consequence.Count);
    }

    [Fact]
    public void Command_Reads_The_Offset_On_Every_Execution_Not_On_Creation()
    {
        var consequence = new CountingCommand();
        var offset = new Vector(9, 0, -1, 0);
        var command = new CollisionCommand(Tree(), () => offset, consequence);

        command.Execute();
        offset = new Vector(5, 0, -1, 0);
        command.Execute();
        command.Execute();
        offset = new Vector(7, 0, -1, 0);
        command.Execute();

        Assert.Equal(2, consequence.Count);
    }

    [Fact]
    public void Command_Does_Not_Read_The_Offset_Until_It_Is_Executed()
    {
        var reads = 0;

        var command = new CollisionCommand(Tree(), () => { reads++; return new Vector(0, 0, 0, 0); }, new CountingCommand());

        Assert.Equal(0, reads);
        command.Execute();
        Assert.Equal(1, reads);
    }

    [Fact]
    public void Failing_Consequence_Is_Not_Swallowed()
    {
        var command = new CollisionCommand(Tree(), () => new Vector(5, 0, -1, 0), new FailingCommand());

        Assert.Throws<InvalidOperationException>(command.Execute);
    }

    private sealed class FailingCommand : Lib.ICommand
    {
        public void Execute() => throw new InvalidOperationException();
    }
}
