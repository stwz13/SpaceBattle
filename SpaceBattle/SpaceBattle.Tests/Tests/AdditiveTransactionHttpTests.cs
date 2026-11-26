using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class AdditiveTransactionHttpTests
{
    private const string GameId = "transactions";
    private const string Rules = "/games/" + GameId + "/rules";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_Host_Only_Allows_Opting_In_When_Transactions_Are_Enabled(bool enabled)
    {
        using var host = new Factory(enabled);
        var parameter = Assert.Single(host.Services.GetServices<IGameRuleParameter>(),
            parameter => parameter.Name == "Transactions.Enabled");
        parameter.Validate(JsonSerializer.SerializeToElement(false));
        Action optIn = () => parameter.Validate(JsonSerializer.SerializeToElement(true));
        if (enabled)
            optIn();
        else
            Assert.Throws<ArgumentException>(optIn);
        Assert.Throws<ArgumentException>(() => parameter.Validate(JsonSerializer.SerializeToElement("true")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_Ordinary_Rule_Command_Rolls_Back_Through_FleetOrder_When_Enabled(bool enabled)
    {
        await using var host = new Factory(true);
        using var first = Player(host, "p1");
        using var second = Player(host, "p2");
        using var lobby = await first.PostAsJsonAsync(Rules + "/lobby", new[] { "p1", "p2" });
        lobby.EnsureSuccessStatusCode();
        var burn = GeneratedNames.CommandDependency(typeof(BurnFuelCommand));
        var rotate = GeneratedNames.CommandDependency(typeof(RotateCommand));
        using var proposal = await first.PutAsJsonAsync(Rules, new
        {
            actions = new Dictionary<string, string[]>
            {
                ["Advance"] = ["Test.Advance", burn],
                ["Turn"] = [rotate],
                ["Hit"] = ["Context.ApplyDamage", "Context.DestroyIfDead"]
            },
            parameters = new Dictionary<string, bool> { ["Transactions.Enabled"] = enabled }
        });
        proposal.EnsureSuccessStatusCode();
        using var firstConfirmation = await first.PostAsync(Rules + "/1/confirm", null);
        firstConfirmation.EnsureSuccessStatusCode();
        using var secondConfirmation = await second.PostAsync(Rules + "/1/confirm", null);
        secondConfirmation.EnsureSuccessStatusCode();
        using var start = await first.PostAsync(Rules + "/1/start", null);
        start.EnsureSuccessStatusCode();

        await Send(first, "CreateItem", Ship("atomic"));
        await Send(first, FleetOrderCommand.Name, new { id = "atomic", action = "Advance" });
        await Send(first, FleetOrderCommand.Name, new { id = "atomic", action = "Turn" });

        var game = Assert.IsAssignableFrom<IManagedGameHost>(
            host.Services.GetRequiredService<IServerApplication>().Runtime.Find(GameId));
        await game.Enqueue(() =>
        {
            var items = Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);
            Assert.IsType<GameItemStorage>(items);
            var atomic = Assert.IsType<GameItem>(items.Get("atomic"));
            Assert.Equal(new Vector(enabled ? 0 : 3, 0), atomic.GetProperty("Position"));
            Assert.Equal(new Angle(1), atomic.GetProperty("Direction"));
            Assert.Equal(false, atomic.GetProperty("Destroyed"));

            var projectile = new GameItem(new Dictionary<string, object> { ["Damage"] = 4 });
            var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = atomic, ["Source"] = projectile });
            Ioc.Resolve<Lib.ICommand>(CommandContextDependencies.Action, "Hit", context).Execute();
            Assert.Equal(0, atomic.GetProperty("Health"));
            Assert.Equal(true, atomic.GetProperty("Destroyed"));
        }).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Old_Ship_Snapshots_Get_The_Default_And_New_Snapshots_Preserve_It()
    {
        var store = new MemorySnapshotStore(new());
        store.Save("legacy", """
            {
              "gameId": "legacy",
              "items": [
                { "id": "active", "kind": "Ship", "properties": { "Fuel": { "type": "int", "value": 5 } } },
                { "id": "destroyed", "kind": "Ship", "properties": {
                  "Fuel": { "type": "int", "value": 1 }, "Destroyed": { "type": "bool", "value": true }
                } },
                { "id": "station", "kind": "Station", "properties": {} }
              ],
              "commands": []
            }
            """);
        var server = ServerComposition.Create(new ServerOptions
        {
            ThreadCount = 1,
            RestoreOnStart = true,
            JwtSecret = AuthenticationTests.Secret,
            ItemKinds = new() { ["Ship"] = ["Fuel"], ["Station"] = [] }
        }, store);

        await server.Start().WaitAsync(Timeout);
        try
        {
            await Assert.IsAssignableFrom<IManagedGameHost>(server.Runtime.Find("legacy")).Enqueue(() =>
            {
                var items = Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);
                Assert.Equal(false, items.Get("active").GetProperty("Destroyed"));
                Assert.Equal(true, items.Get("destroyed").GetProperty("Destroyed"));
                Assert.Throws<KeyNotFoundException>(() => items.Get("station").GetProperty("Destroyed"));
            }).WaitAsync(Timeout);

            var snapshots = await server.Runtime.SnapshotAndStop(store).WaitAsync(Timeout);
            using var snapshot = JsonDocument.Parse(snapshots["legacy"]);
            var items = snapshot.RootElement.GetProperty("items").EnumerateArray()
                .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.GetProperty("properties"));
            Assert.False(items["active"].GetProperty("Destroyed").GetProperty("value").GetBoolean());
            Assert.True(items["destroyed"].GetProperty("Destroyed").GetProperty("value").GetBoolean());
            Assert.Equal(5, items["active"].GetProperty("Fuel").GetProperty("value").GetInt32());
            Assert.False(items["station"].TryGetProperty("Destroyed", out _));
        }
        finally
        {
            await server.Stop().WaitAsync(Timeout);
        }
    }

    private static HttpClient Player(Factory host, string player)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AuthenticationTests.Token(player, GameId));
        return client;
    }

    private static async Task Send(HttpClient client, string command, object args)
    {
        using var response = await client.PostAsJsonAsync("/messages", new { gameId = GameId, command, args });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static object Ship(string id) => new
    {
        id, kind = "Ship", properties = new Dictionary<string, object>
        {
            ["Owner"] = new { type = "string", value = "p1" },
            ["Position"] = new { type = "vector", value = new[] { 0, 0 } },
            ["Direction"] = new { type = "angle", value = 0 },
            ["Speed"] = new { type = "int", value = 3 },
            ["AngularVelocity"] = new { type = "angle", value = 1 },
            ["Fuel"] = new { type = "int", value = 5 },
            ["Health"] = new { type = "int", value = 3 }
        }
    };

    private sealed class Factory(bool enabled) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Transactions:Enabled", enabled.ToString());
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new RegisterIoCDependencyRuleCommand("Test.Advance", args =>
                    new AdvanceCommand(Ioc.Resolve<IMoving>(GeneratedNames.AdapterDependency(typeof(IMoving)), args))));
                services.AddSingleton(new ServerOptions
                {
                    JwtSecret = AuthenticationTests.Secret,
                    AllowedCommands = ["CreateItem", FleetOrderCommand.Name],
                    CommandTargets = new() { ["CreateItem"] = "id", [FleetOrderCommand.Name] = "id" },
                    Players = new[] { "p1", "p2" }.ToDictionary(player => player, _ =>
                        new PlayerAccessOptions { Commands = ["CreateItem", FleetOrderCommand.Name], Items = ["*"] })
                });
            });
        }
    }

    private sealed class AdvanceCommand(IMoving moving) : Lib.ICommand
    {
        public void Execute() => moving.Position = new Vector(3, 0);
    }
}
