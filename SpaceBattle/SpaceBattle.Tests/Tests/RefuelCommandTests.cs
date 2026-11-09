using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class RefuelCommandTests
{
    private static Mock<IRefuelTarget> Target(int fuel)
    {
        var target = new Mock<IRefuelTarget>();
        target.SetupProperty(t => t.Fuel, fuel);
        return target;
    }

    private static Mock<IRefuelSource> Source(int reserve, int amount)
    {
        var source = new Mock<IRefuelSource>();
        source.SetupProperty(s => s.FuelReserve, reserve);
        source.SetupGet(s => s.RefuelAmount).Returns(amount);
        return source;
    }

    [Theory]
    [InlineData(1, 9, 4, 5, 5)]
    [InlineData(0, 4, 4, 4, 0)]
    [InlineData(7, 9, 0, 7, 9)]
    [InlineData(2, 3, 4, 5, 0)]
    [InlineData(2, 0, 4, 2, 0)]
    public void Refuel_Moves_The_Amount_Or_The_Rest_Of_The_Reserve_To_The_Target(int fuel, int reserve, int amount,
        int expectedFuel, int expectedReserve)
    {
        var target = Target(fuel);
        var source = Source(reserve, amount);

        new RefuelCommand(target.Object, source.Object).Execute();

        Assert.Equal(expectedFuel, target.Object.Fuel);
        Assert.Equal(expectedReserve, source.Object.FuelReserve);
    }

    [Fact]
    public void Amount_And_Reserve_Are_Read_When_The_Command_Is_Executed()
    {
        var target = Target(0);
        var source = Source(9, 4);
        var command = new RefuelCommand(target.Object, source.Object);

        command.Execute();
        source.Object.FuelReserve = 1;
        command.Execute();

        Assert.Equal(5, target.Object.Fuel);
        Assert.Equal(0, source.Object.FuelReserve);
    }
}
