using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class BattleContextFactoryTests
{
    private readonly IBattleContextFactory _factory = new BattleContextFactory();

    [Fact]
    public void Factory_Creates_A_Context_With_The_Requested_Id_And_Empty_Storage()
    {
        var context = _factory.Create("Battle-01");

        Assert.Equal("Battle-01", context.Id);
        Assert.Empty(context.Storage.GetSnapshot());
    }

    [Theory]
    [InlineData("First", "Second")]
    [InlineData("Same", "Same")]
    public void Every_Creation_Provides_An_Independent_Context_And_Storage(string firstId, string secondId)
    {
        var first = _factory.Create(firstId);
        var second = _factory.Create(secondId);
        var firstItem = new TestGameItem();
        var secondItem = new TestGameItem();

        Assert.NotSame(first, second);
        Assert.NotSame(first.Storage, second.Storage);
        first.Storage.Add("Ship", firstItem);
        Assert.Empty(second.Storage.GetSnapshot());
        second.Storage.Add("Ship", secondItem);
        Assert.Same(firstItem, first.Storage.Get("Ship"));
        Assert.Same(secondItem, second.Storage.Get("Ship"));
        first.Storage.Remove("Ship");
        Assert.Same(secondItem, second.Storage.Get("Ship"));
        Assert.Equal(firstId, first.Id);
        Assert.Equal(secondId, second.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Factory_Rejects_Invalid_Ids(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => _factory.Create(id!));
    }
}
