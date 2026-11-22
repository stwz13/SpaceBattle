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
    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_Shot_Continues_After_Restore_And_The_Last_Hit_Ends_The_Battle()
    {
        var time = new TickTestClock();
        await using var host = new Factory(time);
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
        await Tick(time, game);
        Assert.Equal(new BattleProgress(false, false, null), await Result(first));
        await Ship(first, "s1", "p1", 0);
        await Ship(first, "ally", "p1", 1);
        await Ship(second, "s2", "p2", 5);
        await game.Enqueue(() => { }).WaitAsync(Timeout);
        await Shoot(first);
        await game.Enqueue(() => { }).WaitAsync(Timeout);
        await Tick(time, game);
        Assert.Equal(new BattleProgress(true, false, null), await Result(first));
        await game.Execute(() =>
        {
            var projectile = Assert.Single(Items().GetSnapshot(), item => Equals(item.GetProperty("Kind"), "Projectile"));
            Assert.Equal(new Vector(3, 0), projectile.GetProperty("Position"));
            Assert.Equal("p1", projectile.GetProperty("Owner"));
            Assert.Equal(false, Items().Get("ally").GetProperty("Destroyed"));
        }).WaitAsync(Timeout);

        var store = new MemorySnapshotStore(new());
        await host.Application.Runtime.SnapshotAndStop(store).WaitAsync(Timeout);
        Assert.Null(time.NextDeadline);
        var restoredTime = new TickTestClock();
        await using var destination = new Factory(restoredTime);
        using var winner = Player(destination, "p1");
        await destination.Application.Runtime.Restore(store).WaitAsync(Timeout);
        var restored = Game(destination);
        await Tick(restoredTime, restored);
        Assert.Equal(new BattleProgress(true, true, "p1"), await Result(winner));
        Assert.Null(restoredTime.NextDeadline);

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
        var finishedTime = new TickTestClock();
        await using var finalHost = new Factory(finishedTime);
        using var spectator = Player(finalHost, "p2");
        await finalHost.Application.Runtime.Restore(finished).WaitAsync(Timeout);
        Assert.Equal(new BattleProgress(true, true, "p1"), await Result(spectator));
        Assert.Null(finishedTime.NextDeadline);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Due_Tick_Does_Not_Run_After_The_Game_Is_Paused_And_Resumes_Once(bool pauseBeforeDelivery)
    {
        var time = new TickTestClock();
        await using var host = new Factory(time);
        using var first = Player(host, "p1");
        using var second = Player(host, "p2");
        await Start(first, second);
        var game = Game(host);
        await Ship(first, "s1", "p1", 0);
        await Ship(second, "s2", "p2", 5);
        await game.Enqueue(() => { }).WaitAsync(Timeout);

        Task<string>? snapshot = null;
        await game.Execute(() =>
        {
            if (pauseBeforeDelivery)
                snapshot = game.PauseAndSnapshot();
            else
                game.Game.Paused = true;

            time.Advance(Period);
            time.FireDue();
            snapshot ??= game.PauseAndSnapshot();
        }).WaitAsync(Timeout);
        var saved = await snapshot!.WaitAsync(Timeout);
        using var json = System.Text.Json.JsonDocument.Parse(saved);
        Assert.Empty(json.RootElement.GetProperty("commands").EnumerateArray());
        await game.Execute(() =>
        {
            Assert.Equal(pauseBeforeDelivery ? 0 : 1, game.Game.Pending.Count);
            Assert.Null(time.NextDeadline);
        }).WaitAsync(Timeout);
        Assert.Equal(new BattleProgress(false, false, null), await Result(first));

        await game.Resume().WaitAsync(Timeout);
        await game.Enqueue(() => { }).WaitAsync(Timeout);
        Assert.Equal(new BattleProgress(false, false, null), await Result(first));
        await Tick(time, game);
        Assert.Equal(new BattleProgress(true, false, null), await Result(first));
    }

    [Fact]
    public async Task A_Failed_Battle_Phase_Stops_The_Tick_At_The_Current_Step()
    {
        var time = new TickTestClock();
        await using var host = new Factory(time);
        using var first = Player(host, "p1");
        using var second = Player(host, "p2");
        await Start(first, second);
        var game = Game(host);
        await Ship(first, "s1", "p1", 0);
        await Ship(second, "s2", "p2", 5);
        await game.Enqueue(() =>
        {
            var broken = new TestGameItem();
            broken.SetProperty("Kind", "Projectile");
            broken.SetProperty("Owner", "p1");
            Items().Add("broken", broken);
        }).WaitAsync(Timeout);

        await Tick(time, game);

        Assert.Null(time.NextDeadline);
        Assert.Equal(new BattleProgress(true, false, null), await Result(first));
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

    private static async Task Tick(TickTestClock time, IManagedGameHost game)
    {
        await game.Execute(() => { }).WaitAsync(Timeout);
        Assert.NotNull(time.NextDeadline);
        time.Advance(Period);
        time.FireDue();
        await game.Enqueue(() => { }).WaitAsync(Timeout);
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

    private sealed class Factory(TickTestClock time) : WebApplicationFactory<Program>
    {
        public IServerApplication Application => Services.GetRequiredService<IServerApplication>();

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(time);
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
