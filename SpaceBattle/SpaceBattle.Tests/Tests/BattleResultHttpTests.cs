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

public sealed class BattleResultHttpTests
{
    private const string GameId = "battle-demo";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_Shot_Continues_After_Restore_And_The_Last_Hit_Ends_The_Battle()
    {
        await using var host = new Factory();
        using var first = Player(host, "p1");
        using var second = Player(host, "p2");
        using var anonymous = host.CreateClient();
        using var wrongGame = Player(host, "p1", "other");
        using var unauthorized = await anonymous.GetAsync($"/games/{GameId}/result");
        using var forbidden = await wrongGame.GetAsync($"/games/{GameId}/result");
        using var missing = await first.GetAsync($"/games/{GameId}/result");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        await Start(first, second);
        var game = Game(host);
        Assert.Equal(new BattleProgress(false, false, null), await Result(first));
        await Ship(first, "s1", "p1", 0);
        await Ship(first, "ally", "p1", 1);
        await Ship(second, "s2", "p2", 1_000_000_000);
        await game.Enqueue(() => { }).WaitAsync(Timeout);
        await Shoot(first);
        await Eventually(async () =>
        {
            var hasMoved = false;
            await game.Enqueue(() =>
            {
                var projectile = Items().GetSnapshot().SingleOrDefault(IsProjectile);
                hasMoved = projectile is not null && ((Vector)projectile.GetProperty("Position")).Coordinates[0] > 0;
                Assert.Equal(false, Items().Get("ally").GetProperty("Destroyed"));
            }).WaitAsync(Timeout);
            return hasMoved;
        });
        Assert.Equal(new BattleProgress(true, false, null), await Result(first));

        var store = new MemorySnapshotStore(new());
        await host.Application.Runtime.SnapshotAndStop(store).WaitAsync(Timeout);
        await using var destination = new Factory();
        using var winner = Player(destination, "p1");
        await destination.Application.Runtime.Restore(store).WaitAsync(Timeout);
        var restored = Game(destination);
        await restored.Enqueue(() =>
        {
            var projectile = Assert.Single(Items().GetSnapshot(), IsProjectile);
            Assert.Equal("p1", projectile.GetProperty("Owner"));
            var position = (Vector)projectile.GetProperty("Position");
            Items().Get("s2").SetProperty("Position", position + new Vector(6, 0));
        }).WaitAsync(Timeout);
        await Eventually(async () => await Result(winner) == new BattleProgress(true, true, "p1"));

        await Shoot(winner);
        await restored.Enqueue(() =>
        {
            Assert.Equal(1, Items().Get("s1").GetProperty("Ammo"));
            Assert.Equal(9, Items().Get("s2").GetProperty("Health"));
            Assert.Equal(true, Items().Get("s2").GetProperty("Destroyed"));
            Assert.Equal(3, Items().GetSnapshot().Count);
        }).WaitAsync(Timeout);

        var finished = new MemorySnapshotStore(new());
        await destination.Application.Runtime.SnapshotAndStop(finished).WaitAsync(Timeout);
        await using var finalHost = new Factory();
        using var spectator = Player(finalHost, "p2");
        await finalHost.Application.Runtime.Restore(finished).WaitAsync(Timeout);
        Assert.Equal(new BattleProgress(true, true, "p1"), await Result(spectator));
        await Game(finalHost).Enqueue(() => Assert.Empty(Game(finalHost).Game.Pending)).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Restore_Checks_A_Path_That_Was_Not_Yet_Consumed_By_The_Tick()
    {
        await using var host = new Factory();
        using var first = Player(host, "p1");
        using var second = Player(host, "p2");
        await Start(first, second);
        var game = Game(host);
        await Ship(first, "s1", "p1", 0);
        await Ship(second, "s2", "p2", 5);
        await Eventually(async () => (await Result(first))?.HasStarted == true);
        await game.Enqueue(() =>
        {
            var items = Items();
            var factory = Ioc.Resolve<IProjectileFactory>(ShootingDependencies.Factory, items.Get("s1"));
            var projectile = factory.Create(new Vector(10, 0), new Angle(0), 3, 1);
            items.Add((string)projectile.GetProperty("Id"), projectile);
            Ioc.Resolve<WorldMovementSegments>(WorldCollisionDependencies.MovementSegments)
                .Record(projectile, new Vector(0, 0), new Vector(10, 0));
            game.Game.Paused = true;
        }).WaitAsync(Timeout);

        var store = new MemorySnapshotStore(new());
        await host.Application.Runtime.SnapshotAndStop(store).WaitAsync(Timeout);
        await using var destination = new Factory();
        using var winner = Player(destination, "p1");
        await destination.Application.Runtime.Restore(store).WaitAsync(Timeout);
        await Eventually(async () => await Result(winner) == new BattleProgress(true, true, "p1"));
        await Game(destination).Enqueue(() =>
        {
            Assert.DoesNotContain(Items().GetSnapshot(), IsProjectile);
            Assert.Equal(9, Items().Get("s2").GetProperty("Health"));
        }).WaitAsync(Timeout);
    }

    private static async Task Start(HttpClient first, HttpClient second)
    {
        var path = $"/games/{GameId}/rules";
        using var lobby = await first.PostAsJsonAsync(path + "/lobby", new[] { "p1", "p2" });
        lobby.EnsureSuccessStatusCode();
        using var proposal = await first.PutAsJsonAsync(path, new Dictionary<string, string[]>());
        proposal.EnsureSuccessStatusCode();
        using var confirm1 = await first.PostAsync(path + "/1/confirm", null);
        using var confirm2 = await second.PostAsync(path + "/1/confirm", null);
        confirm1.EnsureSuccessStatusCode();
        confirm2.EnsureSuccessStatusCode();
        using var start = await first.PostAsync(path + "/1/start", null);
        start.EnsureSuccessStatusCode();
    }

    private static async Task Ship(HttpClient client, string id, string owner, int x) =>
        await Send(client, "CreateItem", new
        {
            id,
            kind = "Ship",
            properties = new Dictionary<string, object>
            {
                ["Owner"] = new { type = "string", value = owner },
                ["Position"] = new { type = "vector", value = new[] { x, 0 } },
                ["Direction"] = new { type = "angle", value = 0 },
                ["Destroyed"] = new { type = "bool", value = false },
                ["Health"] = new { type = "int", value = 10 },
                ["Ammo"] = new { type = "int", value = 2 },
                ["ProjectileSpeed"] = new { type = "int", value = 3 },
                ["ProjectileDamage"] = new { type = "int", value = 1 }
            }
        });

    private static Task Shoot(HttpClient client) => Send(client, FleetOrderCommand.Name, new { id = "s1", action = "Shooting" });

    private static async Task Send(HttpClient client, string command, object args)
    {
        using var response = await client.PostAsJsonAsync("/messages", new { gameId = GameId, command, args });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static bool IsProjectile(IGameItem item) => Equals(item.GetProperty("Kind"), "Projectile");

    private static async Task Eventually(Func<Task<bool>> completed)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!await completed())
            await Task.Delay(10, timeout.Token);
    }

    private static Task<BattleProgress?> Result(HttpClient client) => client.GetFromJsonAsync<BattleProgress>($"/games/{GameId}/result");

    private static IManagedGameHost Game(Factory host) =>
        Assert.IsAssignableFrom<IManagedGameHost>(host.Application.Runtime.Find(GameId));
    private static IGameItemStorage Items() => Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);

    private static HttpClient Player(Factory host, string player, string game = GameId)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthenticationTests.Token(player, game));
        return client;
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        public IServerApplication Application => Services.GetRequiredService<IServerApplication>();

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.AddSingleton(new ServerOptions
            {
                JwtSecret = AuthenticationTests.Secret,
                AllowedCommands = ["CreateItem", FleetOrderCommand.Name],
                CommandTargets = new() { ["CreateItem"] = "id", [FleetOrderCommand.Name] = "id" },
                Players = new[] { "p1", "p2" }.ToDictionary(player => player, _ =>
                    new PlayerAccessOptions { Commands = ["CreateItem", FleetOrderCommand.Name], Items = ["*"] }),
                ItemKinds = new()
                {
                    ["Ship"] = ["Owner", "Position", "Direction", "Destroyed", "Health", "Ammo", "ProjectileSpeed", "ProjectileDamage"],
                    ["Projectile"] = ["Owner", "Position", "Direction", "Speed", "Velocity", "Damage", "Destroyed"]
                }
            });
        });
    }
}
