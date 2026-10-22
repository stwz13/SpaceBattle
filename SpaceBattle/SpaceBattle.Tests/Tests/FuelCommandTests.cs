using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class FuelCommandTests
{
    private static Mock<IFuelCheckable> Checkable(int fuel, int consumption)
    {
        var obj = new Mock<IFuelCheckable>();
        obj.SetupGet(o => o.Fuel).Returns(fuel);
        obj.SetupGet(o => o.FuelConsumption).Returns(consumption);
        return obj;
    }

    [Theory]
    [InlineData(10, 3)]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    public void Check_Passes_When_Fuel_Is_Enough(int fuel, int consumption)
    {
        var command = new CheckFuelCommand(Checkable(fuel, consumption).Object);

        command.Execute();
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(0, 1)]
    public void Check_Throws_When_Fuel_Is_Not_Enough(int fuel, int consumption)
    {
        var command = new CheckFuelCommand(Checkable(fuel, consumption).Object);

        var error = Assert.Throws<InvalidOperationException>(command.Execute);
        Assert.Equal("Not enough fuel to move.", error.Message);
    }

    [Fact]
    public void Burn_Subtracts_Consumption_From_Fuel()
    {
        var obj = new Mock<IFuelBurnable>();
        obj.SetupGet(o => o.Fuel).Returns(10);
        obj.SetupGet(o => o.FuelConsumption).Returns(3);

        new BurnFuelCommand(obj.Object).Execute();

        obj.VerifySet(o => o.Fuel = 7, Times.Once);
    }
}
