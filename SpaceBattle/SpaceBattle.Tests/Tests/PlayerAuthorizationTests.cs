using System.Text.Json;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class PlayerAuthorizationTests
{
    [Theory]
    [InlineData("p", "s", true)]
    [InlineData("other", "s", false)]
    [InlineData("p", "forbidden", false)]
    public async Task Only_The_Configured_Player_Can_Change_An_Owned_Item(string player, string itemId, bool allowed)
    {
        await using var fixture = new RuntimeFixture();
        AuthenticationTests.Configure(fixture);
        using (fixture.Scope.Enter())
        {
            RuntimeFixture.Register("Messages.Command.SetFuel", args =>
            {
                var message = (IMessage)args[0];
                return new SetPropertyCommand(Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get(message.Args.GetProperty("id").GetString()!), "Fuel", 99);
            });
            new RegisterIoCDependencyPlayerAuthorization(new Dictionary<string, string> { ["SetFuel"] = "id" },
                new Dictionary<string, PlayerAccessOptions>
                {
                    ["p"] = new() { Commands = ["SetFuel"], Items = ["s"] },
                    ["other"] = new() { Commands = ["SetFuel"], Items = ["s"] }
                }).Execute();
        }
        var host = fixture.Runtime.Start("a");
        await host.Enqueue(() =>
        {
            foreach (var id in new[] { "s", "forbidden" })
                Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship", id,
                    new[] { KeyValuePair.Create("Fuel", (object)1), KeyValuePair.Create("Owner", (object)"p") }.AsEnumerable());
        });
        host.Send(new AuthenticatedMessage(new Message("a", "SetFuel", JsonSerializer.SerializeToElement(new { id = itemId })), player));
        await host.Enqueue(() => Assert.Equal(allowed ? 99 : 1,
            Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get(itemId).GetProperty("Fuel")));
        if (!allowed) Assert.IsType<UnauthorizedAccessException>(Assert.Single(fixture.Errors.Handled).Exception);
        else Assert.Empty(fixture.Errors.Handled);
    }

    [Fact]
    public void Creation_Cannot_Forge_Another_Players_Ownership()
    {
        var rules = new PlayerCommandRules(
            new PlayerCommandPermissions(new Dictionary<string, IReadOnlySet<string>> { ["p"] = new HashSet<string> { "CreateItem" } }),
            new TargetItemResolver(new Dictionary<string, string> { ["CreateItem"] = "id" }),
            new PlayerItemPermissions(new Dictionary<string, IReadOnlySet<string>> { ["p"] = new HashSet<string> { "s" } }),
            new Dictionary<string, IReadOnlyList<IAccessRule>> { ["CreateItem"] = new IAccessRule[] { new NewItemOwnerRule() } });
        var message = new AuthenticatedMessage(new Message("a", "CreateItem", JsonSerializer.SerializeToElement(new
        {
            id = "s",
            properties = new { Owner = new { type = "string", value = "other" } }
        })), "p");
        Assert.Throws<UnauthorizedAccessException>(() => rules.Validate(message, new ScopedItemOwners(() => new GameItemStorage())));
    }
}
