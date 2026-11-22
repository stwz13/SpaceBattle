using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class FleetOrderHttpTests
{
    private const string GameId = "fleet-match";
    private const string Rules = "/games/" + GameId + "/rules";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Fleet_Orders_Use_The_Authenticated_Player_And_Survive_Restore()
    {
        await using var host = new Factory();
        using var first = Player(host, "p1");
        using var second = Player(host, "p2");
        using var outsider = Player(host, "guest");
        using var lobby = await first.PostAsJsonAsync(Rules + "/lobby", new[] { "p1", "p2" });
        lobby.EnsureSuccessStatusCode();
        var move = GeneratedNames.CommandDependency(typeof(MoveCommand));
        using var agreement = await first.PutAsJsonAsync(Rules, new
        {
            actions = new Dictionary<string, string[]> { ["DoubleMove"] = [move, move], ["SupportRefuel"] = ["Context.Refuel"] },
            parameters = new Dictionary<string, object> { ["objects.Ship"] = new { speed = 3, turnSteps = 2 } }
        });
        agreement.EnsureSuccessStatusCode();
        using var firstConfirmation = await first.PostAsync(Rules + "/1/confirm", null);
        using var secondConfirmation = await second.PostAsync(Rules + "/1/confirm", null);
        using var start = await second.PostAsync(Rules + "/1/start", null);
        firstConfirmation.EnsureSuccessStatusCode();
        secondConfirmation.EnsureSuccessStatusCode();
        start.EnsureSuccessStatusCode();

        await Send(first, "CreateItem", Ship("a", "p1"));
        await Send(first, "CreateItem", Ship("b", "p1"));
        await Send(second, "CreateItem", Ship("c", "p2"));
        await Send(outsider, "CreateItem", Ship("d", "guest"));
        await Send(first, "CreateItem", Tanker());
        var game = Game(host);
        await game.Enqueue(() => Assert.Equal(5, Items().GetSnapshot().Count)).WaitAsync(Timeout);

        await Send(first, FleetOrderCommand.Name, new { id = "a", action = "DoubleMove" });
        await Send(first, FleetOrderCommand.Name, new { id = "b", action = "Rotation" });
        await Send(second, FleetOrderCommand.Name, new { id = "a", action = "Rotation", playerId = "p1" });
        await Send(outsider, FleetOrderCommand.Name, new { id = "d", action = "Rotation" });
        await Send(first, FleetOrderCommand.Name, new { id = "a", action = false });
        await Send(first, FleetOrderCommand.Name, new { id = "a", action = "Refuel" });
        await Send(first, FleetOrderCommand.Name, new { id = "b", action = "Rotation", roles = new { Target = "a" } });
        await Send(first, FleetOrderCommand.Name, new { id = "tanker", action = "Refuel", roles = new[] { "a" } });
        await Send(first, FleetOrderCommand.Name, new { id = "tanker", action = "Refuel", roles = new { Target = 1 } });
        await Send(first, FleetOrderCommand.Name, new { id = "tanker", action = "Refuel",
            roles = new Dictionary<string, string> { ["Target"] = "c" } });
        await Send(first, FleetOrderCommand.Name, new { id = "tanker", action = "Refuel",
            roles = new Dictionary<string, string> { ["Target"] = "a" } });
        await Send(first, FleetOrderCommand.Name, new { id = "tanker", action = "SupportRefuel",
            roles = new Dictionary<string, string> { ["Target"] = "b" } });
        await game.Enqueue(() =>
        {
            var items = Items();
            Assert.Equal(new Vector(6, 0), items.Get("a").GetProperty("Position"));
            Assert.Equal(new Angle(0), items.Get("a").GetProperty("Direction"));
            Assert.Equal(new Angle(2), items.Get("b").GetProperty("Direction"));
            Assert.Equal(new Angle(0), items.Get("d").GetProperty("Direction"));
            Assert.Equal(25, items.Get("a").GetProperty("Fuel"));
            Assert.Equal(25, items.Get("b").GetProperty("Fuel"));
            Assert.Equal(5, items.Get("c").GetProperty("Fuel"));
            Assert.Equal(20, items.Get("tanker").GetProperty("FuelReserve"));
            var factory = Ioc.Resolve<IAuthorizedActionFactory>(AuthorizationDependencies.Factory);
            Assert.Throws<UnauthorizedAccessException>(() => factory.Create("p1", "Rotation", items.Get("c")).Execute());
            Assert.Throws<UnauthorizedAccessException>(() => factory.Create("p1", "Rotation",
                new GameItem(new Dictionary<string, object>())).Execute());
            Assert.Throws<UnauthorizedAccessException>(() => Ioc.Resolve<Lib.ICommand>(
                MessageDependencies.CommandKey(FleetOrderCommand.Name), new Message(GameId, FleetOrderCommand.Name, default)));
        }).WaitAsync(Timeout);

        var store = new MemorySnapshotStore(new());
        await host.Application.Runtime.SnapshotAndStop(store).WaitAsync(Timeout);
        await using var destination = new Factory();
        using var restoredPlayer = Player(destination, "p2");
        using var restoredOutsider = Player(destination, "guest");
        using var restoredSource = Player(destination, "p1");
        await destination.Application.Runtime.Restore(store).WaitAsync(Timeout);
        await Send(restoredPlayer, FleetOrderCommand.Name, new { id = "c", action = "Rotation" });
        await Send(restoredOutsider, FleetOrderCommand.Name, new { id = "d", action = "Rotation" });
        await Send(restoredSource, FleetOrderCommand.Name, new { id = "tanker", action = "Refuel",
            roles = new Dictionary<string, string> { ["Target"] = "a" } });
        await Game(destination).Enqueue(() =>
        {
            Assert.Equal(new Angle(2), Items().Get("c").GetProperty("Direction"));
            Assert.Equal(new Angle(0), Items().Get("d").GetProperty("Direction"));
            Assert.Equal(45, Items().Get("a").GetProperty("Fuel"));
            Assert.Equal(0, Items().Get("tanker").GetProperty("FuelReserve"));
        }).WaitAsync(Timeout);
    }

    private static IManagedGameHost Game(Factory host) =>
        Assert.IsAssignableFrom<IManagedGameHost>(host.Application.Runtime.Find(GameId));

    private static IGameItemStorage Items() => Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);

    private static HttpClient Player(Factory host, string player)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthenticationTests.Token(player, GameId));
        return client;
    }

    private static async Task Send(HttpClient client, string command, object args)
    {
        using var response = await client.PostAsJsonAsync("/messages", new { gameId = GameId, command, args });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static object Ship(string id, string player) => new
    {
        id, kind = "Ship", properties = new Dictionary<string, object>
        {
            ["Owner"] = new { type = "string", value = player },
            ["Position"] = new { type = "vector", value = new[] { 0, 0 } },
            ["Direction"] = new { type = "angle", value = 0 },
            ["Speed"] = new { type = "int", value = 1 },
            ["AngularVelocity"] = new { type = "angle", value = 1 },
            ["Fuel"] = new { type = "int", value = 5 }
        }
    };

    private static object Tanker() => new
    {
        id = "tanker", kind = "Tanker", properties = new Dictionary<string, object>
        {
            ["Owner"] = new { type = "string", value = "p1" },
            ["FuelReserve"] = new { type = "int", value = 60 },
            ["RefuelAmount"] = new { type = "int", value = 20 }
        }
    };

    private sealed class Factory : WebApplicationFactory<Program>
    {
        public IServerApplication Application => Services.GetRequiredService<IServerApplication>();

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.AddSingleton(new FleetContextOrder("SupportRefuel", "Source", ["Target"]));
            services.AddSingleton(new ServerOptions
            {
                JwtSecret = AuthenticationTests.Secret,
                AllowedCommands = ["CreateItem", FleetOrderCommand.Name],
                CommandTargets = new() { ["CreateItem"] = "id", [FleetOrderCommand.Name] = "id" },
                Players = new[] { "p1", "p2", "guest" }.ToDictionary(player => player, _ =>
                    new PlayerAccessOptions { Commands = ["CreateItem", FleetOrderCommand.Name], Items = ["*"] }),
                ItemKinds = new()
                {
                    ["Ship"] = ["Owner", "Position", "Direction", "Speed", "AngularVelocity", "Fuel"],
                    ["Tanker"] = ["Owner", "FuelReserve", "RefuelAmount"]
                }
            });
        });
    }
}
