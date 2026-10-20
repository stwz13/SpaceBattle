using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class DamageCommandTests
{
    private static ApplyDamageCommand Damage(Mock<IDamageTarget> target, int damage)
    {
        var source = new Mock<IDamageSource>();
        source.SetupGet(s => s.Damage).Returns(damage);
        return new ApplyDamageCommand(target.Object, source.Object);
    }

    private static Mock<IDamageTarget> Target(int health)
    {
        var target = new Mock<IDamageTarget>();
        target.SetupProperty(t => t.Health, health);
        return target;
    }

    [Theory]
    [InlineData(10, 3, 7)]
    [InlineData(10, 10, 0)]
    [InlineData(10, 25, 0)]
    [InlineData(10, 0, 10)]
    public void Damage_Reduces_Health_And_Never_Goes_Below_Zero(int health, int damage, int expected)
    {
        var target = Target(health);

        Damage(target, damage).Execute();

        Assert.Equal(expected, target.Object.Health);
    }

    [Fact]
    public void Damage_Is_Read_From_The_Source_When_Executed()
    {
        var target = Target(10);
        var source = new Mock<IDamageSource>();
        source.SetupGet(s => s.Damage).Returns(4);
        var command = new ApplyDamageCommand(target.Object, source.Object);

        command.Execute();
        command.Execute();

        Assert.Equal(2, target.Object.Health);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public void Item_Is_Destroyed_Only_Without_Health(int health, bool destroyed)
    {
        var item = new Mock<IDestructible>();
        item.SetupGet(i => i.Health).Returns(health);
        item.SetupProperty(i => i.Destroyed, false);

        new DestroyIfDeadCommand(item.Object).Execute();

        Assert.Equal(destroyed, item.Object.Destroyed);
    }
}
