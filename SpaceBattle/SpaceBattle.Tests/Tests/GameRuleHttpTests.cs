using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class GameRuleHttpTests
{
    private const string Path = "/games/match/rules";

    [Fact]
    public async Task Players_Agree_To_Future_Command_And_Rules_Survive_Restore()
    {
        await using var host = new Factory();
        using var client = host.CreateClient();
        using var guest = host.CreateClient();
        using var anonymous = await guest.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        SignIn(guest, "p1", "other");
        using var wrongGame = await guest.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, wrongGame.StatusCode);
        SignIn(client, "p1");
        using var absent = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        using var badRoster = await client.PostAsJsonAsync(Path + "/lobby", new[] { "p2" });
        Assert.Equal(HttpStatusCode.Forbidden, badRoster.StatusCode);
        using var created = await client.PostAsJsonAsync(Path + "/lobby", new[] { "p1", "p2" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var duplicate = await client.PostAsJsonAsync(Path + "/lobby", new[] { "p1" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        SignIn(guest, "outsider");
        using var privateView = await guest.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, privateView.StatusCode);
        using var malformed = await client.PutAsJsonAsync(Path, "wrong-shape");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var invalid = await client.PutAsJsonAsync(Path, new Dictionary<string, string[]> { ["Jump"] = [" "] });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        string[] steps = ["Commands.SpaceBattle_Lib_MoveCommand", "Mods.NewWarp"];
        var rules = new Dictionary<string, string[]>
        {
            ["Custom.Jump"] = steps,
            ["Custom.Push"] = ["Mods.Push", "Mods.DefaultPush"]
        };
        var definition = new { actions = rules, parameters = new Dictionary<string, object>
        {
            ["Ship.Speed"] = 1, ["Shooting.Projectile"] = new { speed = 8, damage = 4 },
            ["Hull.Settings"] = new { strength = 15 },
            ["objects.Ship"] = new { speed = 3, turnSteps = 2 },
            ["objects.Probe"] = new { speed = 11, turnSteps = 1 },
            ["commands.Mods.Push"] = new { distance = 20 }, ["commands.Mods.NewWarp"] = new { distance = 10 }
        } };
        using var proposal = await client.PutAsJsonAsync(Path, definition);
        proposal.EnsureSuccessStatusCode();
        var revision = (await proposal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64();
        var current = $"{Path}/{revision}";
        using var confirm = await client.PostAsync(current + "/confirm", null);
        confirm.EnsureSuccessStatusCode();
        using var badSpeed = await client.PutAsJsonAsync(Path, new
            { parameters = new Dictionary<string, int> { ["Ship.Speed"] = -1 } });
        Assert.Equal(HttpStatusCode.BadRequest, badSpeed.StatusCode);
        using var badHull = await client.PutAsJsonAsync(Path, new
            { parameters = new Dictionary<string, object> { ["Hull.Settings"] = new { } } });
        Assert.Equal(HttpStatusCode.BadRequest, badHull.StatusCode);
        using var badCommandSetting = await client.PutAsJsonAsync(Path, new
            { parameters = new Dictionary<string, object> { ["commands.Mods.Push"] = new { distance = -1 } } });
        Assert.Equal(HttpStatusCode.BadRequest, badCommandSetting.StatusCode);
        foreach (var settings in new object[] { 2, new { speed = -1 }, new { turnSteps = "90°" }, new { ammo = 7 } })
        {
            using var invalidObject = await client.PutAsJsonAsync(Path, new
                { parameters = new Dictionary<string, object> { ["objects.Probe"] = settings } });
            Assert.Equal(HttpStatusCode.BadRequest, invalidObject.StatusCode);
        }
        using var early = await client.PostAsync(current + "/start", null);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        SignIn(guest, "p2");
        var view = await guest.GetFromJsonAsync<JsonElement>(Path);
        Assert.Equal("Mods.NewWarp", view.GetProperty("rules").GetProperty("actions").GetProperty("Custom.Jump")[1].GetString());
        var parameters = view.GetProperty("rules").GetProperty("parameters");
        Assert.Equal(1, parameters.GetProperty("Ship.Speed").GetInt32());
        Assert.Equal(10, parameters.GetProperty("commands.Mods.NewWarp").GetProperty("distance").GetInt32());
        using var second = await guest.PostAsync(current + "/confirm", null);
        second.EnsureSuccessStatusCode();
        using var start = await guest.PostAsync(current + "/start", null);
        start.EnsureSuccessStatusCode();
        using var late = await client.PutAsJsonAsync(Path, rules);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);

        var snapshots = new MemorySnapshotStore(new());
        await host.Application.Runtime.SnapshotAndStop(snapshots).WaitAsync(TimeSpan.FromSeconds(10));
        await using var destination = new Factory();
        using var destinationClient = destination.CreateClient();
        await destination.Application.Runtime.Restore(snapshots).WaitAsync(TimeSpan.FromSeconds(10));
        var restored = Assert.IsAssignableFrom<IManagedGameHost>(destination.Application.Runtime.Find("match"));
        await restored.Execute(() =>
        {
            var ship = new GameItem(new Dictionary<string, object>
            {
                ["Kind"] = "Ship", ["Position"] = new Vector(0, 0), ["Speed"] = 2, ["Direction"] = new Angle(0)
            });
            Assert.Throws<KeyNotFoundException>(() => Ioc.Resolve<Lib.ICommand>("Commands.Action", "Custom.Jump", ship));
            PushRegistration("Mods.NewWarp", 0).Execute();
            Ioc.Resolve<Lib.ICommand>("Commands.Action", "Custom.Jump", ship).Execute();
            Assert.Equal(new Vector(13, 0), ship.GetProperty("Position"));
            ship.SetProperty("Speed", 99);
            var jump = Ioc.Resolve<Lib.ICommand>("Commands.Action", "Custom.Jump", ship);
            jump.Execute();
            Assert.Equal(new Vector(26, 0), ship.GetProperty("Position"));
            ship.SetProperty("Direction", new Angle(2));
            jump.Execute();
            Assert.Equal(new Vector(36, 3), ship.GetProperty("Position"));
            var probe = new GameItem(new Dictionary<string, object>
                { ["Kind"] = "Probe", ["Position"] = new Vector(0, 0), ["Speed"] = 7, ["Direction"] = new Angle(0) });
            Ioc.Resolve<Lib.ICommand>("Commands.Action", "Custom.Jump", probe).Execute();
            Assert.Equal(new Vector(21, 0), probe.GetProperty("Position"));
            Ioc.Resolve<Lib.ICommand>("Commands.Action", "Custom.Push", probe).Execute();
            Assert.Equal(new Vector(43, 0), probe.GetProperty("Position"));
            var asteroid = new GameItem(new Dictionary<string, object>
            {
                ["Kind"] = "Asteroid", ["Position"] = new Vector(0, 0), ["Speed"] = 7,
                ["Direction"] = new Angle(0), ["AngularVelocity"] = new Angle(3)
            });
            Ioc.Resolve<Lib.ICommand>("Commands.Action", "Custom.Jump", asteroid).Execute();
            Assert.Equal(new Vector(17, 0), asteroid.GetProperty("Position"));
            foreach (var (item, direction) in new[] { (ship, 4), (probe, 1), (asteroid, 3) })
            {
                Ioc.Resolve<Lib.ICommand>("Commands.Action", "Rotation", item).Execute();
                Assert.Equal(new Angle(direction), item.GetProperty("Direction"));
            }
            ship.SetProperty("Ammo", 1);
            Ioc.Resolve<Lib.ICommand>("Commands.Action", "Shooting", ship).Execute();
            var storage = Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);
            var projectile = Assert.Single(storage.GetSnapshot());
            Assert.Equal(8, projectile.GetProperty("Speed"));
            Assert.Equal(4, projectile.GetProperty("Damage"));
            Assert.Equal(0, ship.GetProperty("Ammo"));
            Assert.Equal(15, Ioc.Resolve<IHullSettings>("Tests.HullSettings").Strength);
        }).WaitAsync(TimeSpan.FromSeconds(10));
        SignIn(destinationClient, "p1");
        using var reopen = await destinationClient.PostAsJsonAsync(Path + "/lobby", new[] { "p1" });
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
    }

    [Fact]
    public async Task A_New_Readable_Property_Can_Be_Exposed_By_Schema_Alone()
    {
        var schema = System.IO.Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(schema, """
                {
                  "projectileDamage": {
                    "view": "SpaceBattle.Lib.IShooting",
                    "property": "ProjectileDamage",
                    "type": "integer"
                  }
                }
                """);
            await using var host = new Factory(schema);
            using var client = host.CreateClient();
            SignIn(client, "p1");
            using var lobby = await client.PostAsJsonAsync(Path + "/lobby", new[] { "p1" });
            lobby.EnsureSuccessStatusCode();
            using var proposal = await client.PutAsJsonAsync(Path, new
            {
                parameters = new Dictionary<string, object>
                {
                    ["objects.Frigate"] = new { projectileDamage = 12 },
                    ["Shooting.Projectile"] = new { speed = 8, damage = 4 }
                }
            });
            proposal.EnsureSuccessStatusCode();
            using var confirmation = await client.PostAsync(Path + "/1/confirm", null);
            confirmation.EnsureSuccessStatusCode();
            using var start = await client.PostAsync(Path + "/1/start", null);
            start.EnsureSuccessStatusCode();
            var game = Assert.IsAssignableFrom<IManagedGameHost>(host.Application.Runtime.Find("match"));
            await game.Execute(() =>
            {
                foreach (var (kind, damage) in new[] { ("Frigate", 12), ("Ship", 4) })
                {
                    var item = new GameItem(new Dictionary<string, object> { ["Kind"] = kind });
                    var shooting = Ioc.Resolve<IShooting>(GeneratedNames.AdapterDependency(typeof(IShooting)), item);
                    Assert.Equal(damage, shooting.ProjectileDamage);
                    Assert.Equal(8, shooting.ProjectileSpeed);
                }
            }).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { File.Delete(schema); }
    }

    private static void SignIn(HttpClient client, string player, string game = "match") =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AuthenticationTests.Token(player, game));

    private static RegisterIoCDependencyRuleCommand PushRegistration(string name, int defaultDistance) =>
        RegisterIoCDependencyRuleCommand.For<IGameItem, IMoving, int>(name,
            item => Ioc.Resolve<IMoving>(GeneratedNames.AdapterDependency(typeof(IMoving)), item),
            new PushFactory(), ReadDistance, defaultDistance);

    private static int ReadDistance(JsonElement value)
    {
        var distance = value.GetProperty("distance").GetInt32();
        ArgumentOutOfRangeException.ThrowIfNegative(distance);
        return distance;
    }

    private sealed class PushFactory : IConfiguredRuleCommandFactory<IMoving, int>
    {
        public Lib.ICommand Create(IMoving target, int settings) =>
            new CallbackCommand(() => target.Position += new Vector(settings, 0));
    }

    private sealed class Factory(string? schemaPath = null) : WebApplicationFactory<Program>
    {
        public IServerApplication Application => Services.GetRequiredService<IServerApplication>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (schemaPath is not null)
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["GameRules:ObjectSchema"] = schemaPath }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new ServerOptions { JwtSecret = AuthenticationTests.Secret });
                services.AddSingleton(PushRegistration("Mods.Push", 1));
                services.AddSingleton(PushRegistration("Mods.DefaultPush", 2));
                services.AddSingleton<IGameRuleParameter>(new GameClassParameter<IHullSettings>("Hull.Settings",
                    value => new HullSettings(value.GetProperty("strength").GetInt32()),
                    settings => Ioc.Resolve<App.ICommand>("IoC.Register", "Tests.HullSettings",
                        (Func<object[], object>)(_ => settings)).Execute()));
            });
        }
    }

    private interface IHullSettings { int Strength { get; } }
    private sealed record HullSettings(int Strength) : IHullSettings;
}
