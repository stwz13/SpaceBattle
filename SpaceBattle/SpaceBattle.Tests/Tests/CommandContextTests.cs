using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CommandContextTests
{
    [Fact]
    public void Roles_Are_Independent_And_Case_Sensitive()
    {
        var first = new TestGameItem();
        var second = new TestGameItem();
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = first, ["target"] = second });

        Assert.Same(first, context.GetItem("Target"));
        Assert.Same(second, context.GetItem("target"));
        Assert.Throws<KeyNotFoundException>(() => context.GetItem("Source"));
    }

    [Fact]
    public void Changing_The_Source_Dictionary_Does_Not_Replace_Context_Participants()
    {
        var original = new TestGameItem();
        var roles = new Dictionary<string, IGameItem> { ["Target"] = original };
        var context = new CommandContext(roles);

        roles["Target"] = new TestGameItem();
        roles.Clear();
        original.SetProperty("Value", 7);

        Assert.Same(original, context.GetItem("Target"));
        Assert.Equal(7, context.GetItem("Target").GetProperty("Value"));
    }

    [Fact]
    public void The_Same_Item_Can_Occupy_Multiple_Roles()
    {
        var item = new TestGameItem();
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["First"] = item, ["Second"] = item });

        Assert.Same(context.GetItem("First"), context.GetItem("Second"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Invalid_Roles_Are_Rejected_During_Creation_And_Lookup(string? role)
    {
        var context = new CommandContext(Array.Empty<KeyValuePair<string, IGameItem>>());

        Assert.ThrowsAny<ArgumentException>(() => context.GetItem(role!));
        Assert.ThrowsAny<ArgumentException>(() => new CommandContext(
            [new KeyValuePair<string, IGameItem>(role!, new TestGameItem())]));
    }

    [Fact]
    public void Null_Participants_And_Null_Collections_Are_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CommandContext(null!));
        Assert.Throws<ArgumentNullException>(() => new CommandContext(
            [new KeyValuePair<string, IGameItem>("Target", null!)]));
    }

    [Fact]
    public void Duplicate_Roles_Are_Rejected()
    {
        var item = new TestGameItem();

        Assert.Throws<ArgumentException>(() => new CommandContext(
            [new KeyValuePair<string, IGameItem>("Target", item), new KeyValuePair<string, IGameItem>("Target", item)]));
    }
}
