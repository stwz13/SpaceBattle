namespace SpaceBattle.Tests;

using SpaceBattle.Lib;
using Moq;
public class RotateTest
{
    [Fact]
    public void TestRotationChangesObjectDirectionCorrectly()
    {
        Mock<IRotating> rotating = new Mock<IRotating>();
        rotating.SetupGet(a => a.Direction).Returns(SpaceBattle.Tests.TestValues.Angle(1));
        rotating.SetupGet(a => a.AngularVelocity).Returns(SpaceBattle.Tests.TestValues.Angle(1));

        ICommand cmd = new RotateCommand(rotating.Object);
        cmd.Execute();

        rotating.VerifySet(a => a.Direction = SpaceBattle.Tests.TestValues.Angle(2));
    }

    [Fact]
    public void TestRotationWithoutDirectionThrowsException()
    {
        Mock<IRotating> rotating = new Mock<IRotating>();
        rotating.SetupGet(a => a.Direction).Throws<InvalidOperationException>();
        rotating.SetupGet(a => a.AngularVelocity).Returns(SpaceBattle.Tests.TestValues.Angle(1));

        ICommand cmd = new RotateCommand(rotating.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }

    [Fact]
    public void TestRotationWithoutAngularVelocityThrowsException()
    {
        Mock<IRotating> rotating = new Mock<IRotating>();
        rotating.SetupGet(a => a.Direction).Returns(SpaceBattle.Tests.TestValues.Angle(1));
        rotating.SetupGet(a => a.AngularVelocity).Throws<InvalidOperationException>();

        ICommand cmd = new RotateCommand(rotating.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }

    [Fact]
    public void TestImpossibleRotationThrowsException()
    {
        Mock<IRotating> rotating = new Mock<IRotating>();
        rotating.SetupGet(a => a.Direction).Returns(SpaceBattle.Tests.TestValues.Angle(1));
        rotating.SetupGet(a => a.AngularVelocity).Returns(SpaceBattle.Tests.TestValues.Angle(1));

        rotating.SetupSet(a => a.Direction = SpaceBattle.Tests.TestValues.Angle(2)).Throws<InvalidOperationException>();

        ICommand cmd = new RotateCommand(rotating.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }
}
