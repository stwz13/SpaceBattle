using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class RefuelCommandTests
{
    [Theory]
    [InlineData(10, 4)]
    [InlineData(4, 4)]
    [InlineData(0, 0)]
    public void Check_Passes_When_Reserve_Is_Enough(int reserve, int amount)
    {
        var obj = new Mock<IRefuelCheckable>();
        obj.SetupGet(o => o.FuelReserve).Returns(reserve);
        obj.SetupGet(o => o.RefuelAmount).Returns(amount);
        obj.SetupGet(o => o.ExceptionFactory).Returns(new ExceptionFactory());

        new CheckRefuelSourceCommand(obj.Object).Execute();
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(0, 1)]
    public void Check_Throws_When_Reserve_Is_Not_Enough(int reserve, int amount)
    {
        var obj = new Mock<IRefuelCheckable>();
        obj.SetupGet(o => o.FuelReserve).Returns(reserve);
        obj.SetupGet(o => o.RefuelAmount).Returns(amount);
        obj.SetupGet(o => o.ExceptionFactory).Returns(new ExceptionFactory());

        var error = Assert.Throws<InvalidOperationException>(new CheckRefuelSourceCommand(obj.Object).Execute);
        Assert.Equal("Not enough fuel in reserve to refuel.", error.Message);
    }

    [Fact]
    public void Refuel_Moves_Amount_From_Reserve_To_Fuel()
    {
        var ship = new Mock<IRefuelable>();
        ship.SetupProperty(o => o.Fuel, 2);
        ship.SetupProperty(o => o.FuelReserve, 10);
        ship.SetupGet(o => o.RefuelAmount).Returns(4);

        new RefuelCommand(ship.Object).Execute();

        Assert.Equal(6, ship.Object.Fuel);
        Assert.Equal(6, ship.Object.FuelReserve);
    }
}
