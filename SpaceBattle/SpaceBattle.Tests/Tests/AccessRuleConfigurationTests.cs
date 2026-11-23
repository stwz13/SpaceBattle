using System.Text.Json;
using App;
using SpaceBattle.Lib;
namespace SpaceBattle.Tests;

public class AccessRuleConfigurationTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"properties\":[]}")]
    [InlineData("{\"properties\":{\"Owner\":{\"type\":\"int\",\"value\":1}}}")]
    [InlineData("""{"properties":{"Owner":"p"}}""")]
    [InlineData("""{"properties":{"Owner":{"value":"p"}}}""")]
    [InlineData("""{"properties":{"Owner":{"type":1,"value":"p"}}}""")]
    [InlineData("""{"properties":{"Owner":{"type":"string"}}}""")]
    [InlineData("""{"properties":{"Owner":{"type":"string","value":1}}}""")]
    public void Malformed_Owner_Claims_Are_Denied(string json)
    {
        var message = new AuthenticatedMessage(new Message("a", "CreateItem", JsonSerializer.Deserialize<JsonElement>(json)), "p");
        Assert.Throws<UnauthorizedAccessException>(() => new NewItemOwnerRule().Check(message, "s", new ScopedItemOwners(() => new GameItemStorage())));
    }

    [Theory]
    [InlineData("unknown-command")]
    [InlineData("missing-list")]
    [InlineData("empty-list")]
    [InlineData("missing-rule")]
    public void An_Incomplete_Access_Rule_Configuration_Is_Rejected(string problem)
    {
        IReadOnlyList<IAccessRule> rules = problem switch
        {
            "missing-list" => null!,
            "empty-list" => [],
            "missing-rule" => [null!],
            _ => [new ExistingItemOwnerRule()]
        };
        var command = problem == "unknown-command" ? "Unknown" : "Known";
        var registration = new RegisterIoCDependencyPlayerAuthorization(
            new Dictionary<string, string> { ["Known"] = "id" },
            new Dictionary<string, PlayerAccessOptions>(),
            new Dictionary<string, IReadOnlyList<IAccessRule>> { [command] = rules });
        Assert.Throws<ArgumentException>(registration.Execute);
    }

    [Theory]
    [InlineData(" ", "id", "p")]
    [InlineData("Command", " ", "p")]
    [InlineData("Command", "id", " ")]
    public void Empty_Command_Target_And_Player_Names_Are_Rejected(string command, string target, string player)
    {
        var registration = new RegisterIoCDependencyPlayerAuthorization(
            new Dictionary<string, string> { [command] = target },
            new Dictionary<string, PlayerAccessOptions> { [player] = new() });
        Assert.Throws<ArgumentException>(registration.Execute);
    }
    [Fact]
    public async Task A_New_Command_Can_Configure_Its_Rules_Without_Changing_The_Interpreter()
    {
        await using var fixture = new RuntimeFixture(1);
        var order = new List<int>();
        using (fixture.Scope.Enter())
        {
            RuntimeFixture.Register("Messages.Command.Custom", _ => new CallbackCommand(() => order.Add(3)));
            new RegisterIoCDependencyPlayerAuthorization(new Dictionary<string, string> { ["Custom"] = "id" },
                new Dictionary<string, PlayerAccessOptions> { ["p"] = new() { Commands = ["Custom"], Items = ["s"] } },
                new Dictionary<string, IReadOnlyList<IAccessRule>> { ["Custom"] = new IAccessRule[] { new RecordRule(order, 1), new RecordRule(order, 2) } }).Execute();
        }
        var host = fixture.Runtime.Start("a");
        host.Send(new AuthenticatedMessage(new Message("a", "Custom", JsonSerializer.SerializeToElement(new { id = "s" })), "p"));
        await host.Enqueue(() => { });
        Assert.Equal(new[] { 1, 2, 3 }, order);
        Assert.Empty(fixture.Errors.Handled);
    }
    [Fact]
    public async Task Server_Options_Resolve_Configured_Rule_Names_In_Order()
    {
        var order = new List<int>();
        var options = new ServerOptions
        {
            ThreadCount = 1,
            JwtSecret = AuthenticationTests.Secret,
            Games = ["a"],
            AllowedCommands = ["Custom"],
            CommandTargets = new() { ["Custom"] = "id" },
            Players = new() { ["p"] = new() { Commands = ["Custom"], Items = ["s"] } },
            CommandAccessRules = new() { ["Custom"] = ["first", "second"] }
        };
        var app = ServerComposition.Create(options, configureCommands: () =>
        {
            RuntimeFixture.Register(AuthenticationDependencies.AccessRule("first"), _ => new RecordRule(order, 1));
            RuntimeFixture.Register(AuthenticationDependencies.AccessRule("second"), _ => new RecordRule(order, 2));
            RuntimeFixture.Register("Messages.Command.Custom", _ => new CallbackCommand(() => order.Add(3)));
        });
        await app.Start();
        try
        {
            var host = Assert.Single(app.Runtime.Games);
            host.Send(new AuthenticatedMessage(new Message("a", "Custom", JsonSerializer.SerializeToElement(new { id = "s" })), "p"));
            await host.Enqueue(() => { });
            Assert.Equal(new[] { 1, 2, 3 }, order);
        }
        finally { await app.Stop(); }
    }
    private sealed class RecordRule(List<int> order, int value) : IAccessRule
    {
        public void Check(IAuthenticatedMessage message, string itemId, IItemOwners owners) => order.Add(value);
    }
}
