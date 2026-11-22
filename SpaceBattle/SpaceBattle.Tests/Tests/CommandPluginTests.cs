using System.Net;
using App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class CommandPluginTests
{
    [Fact]
    public async Task Restarted_Host_Loads_External_Command_And_Enforces_Item_Ownership()
    {
        var snapshot = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await using (var original = new ConfiguredHost(snapshot, withPlugin: false))
            {
                using var client = original.CreateClient();
                using var created = await HttpApiTests.Post(client,
                    """{"gameId":"demo","command":"CreateItem","args":{"kind":"Ship","id":"s","properties":{"Health":{"type":"int","value":5},"Owner":{"type":"string","value":"owner"}}}}""",
                    AuthenticationTests.Token("owner", "demo"));
                Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
                Assert.Equal(5, await Health(original));
                using var unavailable = await Repair(client, "owner");
                Assert.Equal(HttpStatusCode.BadRequest, unavailable.StatusCode);
            }

            await using var restarted = new ConfiguredHost(snapshot, withPlugin: true);
            using var restartedClient = restarted.CreateClient();
            Assert.Equal(5, await Health(restarted));

            using var allowed = await Repair(restartedClient, "owner");
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
            Assert.Equal(8, await Health(restarted));

            using var denied = await Repair(restartedClient, "other");
            // The endpoint acknowledges queueing; ownership is checked inside the game.
            Assert.Equal(HttpStatusCode.Accepted, denied.StatusCode);
            Assert.Equal(8, await Health(restarted));
        }
        finally { File.Delete(snapshot); }
    }

    [Theory]
    [InlineData("missing-target", "Configure Server:CommandTargets:Repair")]
    [InlineData("no-commands", "exports no command plugins")]
    [InlineData("duplicate", "registered more than once")]
    public void Invalid_Plugin_Configuration_Prevents_Startup(string scenario, string error)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Server:CommandPlugins:0"] = scenario == "no-commands"
                ? typeof(ICommandPlugin).Assembly.Location
                : "TestPlugins/RepairPlugin.dll",
            ["Server:CommandPlugins:1"] = "TestPlugins/RepairPlugin.dll"
        };
        if (scenario == "duplicate") settings["Server:CommandTargets:Repair"] = "shipId";

        var services = new ServiceCollection();
        services.AddLogging();
        HostComposition.Configure(services, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ServerApplication>());
        Assert.Contains(error, exception.Message);
    }

    private static Task<HttpResponseMessage> Repair(HttpClient client, string player) => HttpApiTests.Post(client,
        """{"gameId":"demo","command":"Repair","args":{"shipId":"s","amount":3}}""",
        AuthenticationTests.Token(player, "demo"));

    private static async Task<int> Health(WebApplicationFactory<Program> factory)
    {
        var host = Assert.IsType<ManagedGameHost>(factory.Services
            .GetRequiredService<ServerApplication>().Runtime.Find("demo"));
        var health = 0;
        await host.Enqueue(() => health = (int)Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context)
            .Storage.Get("s").GetProperty("Health")).WaitAsync(TimeSpan.FromSeconds(10));
        return health;
    }

    private sealed class ConfiguredHost(string snapshot, bool withPlugin) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureLogging(logging => logging.ClearProviders()).ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Server:JwtSecret"] = AuthenticationTests.Secret,
                ["Server:SnapshotPath"] = snapshot,
                ["Server:ItemKinds:Ship:0"] = "Health",
                ["Server:Players:owner:Commands:0"] = "CreateItem",
                ["Server:Players:owner:Items:0"] = "s",
                ["Server:Players:other:Commands:0"] = "Repair",
                ["Server:Players:other:Items:0"] = "s"
            };
            if (withPlugin)
            {
                settings["Server:CommandPlugins:0"] = "TestPlugins/RepairPlugin.dll";
                settings["Server:AllowedCommands:2"] = "Repair";
                settings["Server:CommandTargets:Repair"] = "shipId";
                settings["Server:Players:owner:Commands:1"] = "Repair";
            }
            configuration.AddInMemoryCollection(settings);
        });
    }
}
