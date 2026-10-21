using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class BattleContextTests
{
    [Fact]
    public void Context_Preserves_The_Provided_Id_And_Storage_Without_Using_The_Storage()
    {
        var storage = new Mock<IGameItemStorage>(MockBehavior.Strict);

        IBattleContext context = new BattleContext("Battle-01", storage.Object);

        Assert.Equal("Battle-01", context.Id);
        Assert.Same(storage.Object, context.Storage);
        Assert.Same(storage.Object, context.Storage);
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public void Context_Uses_The_Provided_Populated_Storage()
    {
        var storage = new GameItemStorage();
        var item = new TestGameItem();
        storage.Add("Ship", item);
        IBattleContext context = new BattleContext("Battle", storage);

        Assert.Same(item, context.Storage.Get("Ship"));
        Assert.True(context.Storage.Remove("Ship"));
        Assert.Empty(storage.GetSnapshot());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Invalid_Ids_Are_Rejected(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new BattleContext(id!, new GameItemStorage()));
    }

    [Fact]
    public void Null_Storage_Is_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new BattleContext("Battle", null!));
    }
}
