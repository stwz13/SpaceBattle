using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MoveCommandTests
{
    [Fact]
    public void Move_Adds_Velocity_To_Position()
    {
        var moving = new Mock<IMoving>();
        moving.SetupGet(m => m.Position).Returns(new Vector(12, 5));
        moving.SetupGet(m => m.Velocity).Returns(new Vector(-7, 3));

        new MoveCommand(moving.Object).Execute();

        moving.VerifySet(m => m.Position = new Vector(5, 8), Times.Once);
    }

    [Fact]
    public void Move_Without_Position_Fails()
    {
        var moving = new Mock<IMoving>();
        moving.SetupGet(m => m.Position).Throws<InvalidOperationException>();

        Assert.Throws<InvalidOperationException>(new MoveCommand(moving.Object).Execute);
    }

    [Fact]
    public void Move_Without_Velocity_Fails()
    {
        var moving = new Mock<IMoving>();
        moving.SetupGet(m => m.Position).Returns(new Vector(1, 1));
        moving.SetupGet(m => m.Velocity).Throws<InvalidOperationException>();

        Assert.Throws<InvalidOperationException>(new MoveCommand(moving.Object).Execute);
    }

    [Fact]
    public void Move_Fails_When_Position_Cannot_Be_Set()
    {
        var moving = new Mock<IMoving>();
        moving.SetupGet(m => m.Position).Returns(new Vector(1, 1));
        moving.SetupGet(m => m.Velocity).Returns(new Vector(1, 1));
        moving.SetupSet(m => m.Position = It.IsAny<Vector>()).Throws<InvalidOperationException>();

        Assert.Throws<InvalidOperationException>(new MoveCommand(moving.Object).Execute);
    }
}
