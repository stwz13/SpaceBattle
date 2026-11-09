using App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ServerHostTests
{
    [Fact]
    public async Task Real_Composition_Starts_From_Config_Handles_Http_And_Snapshots_On_Shutdown()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await using (var factory = new ConfiguredFactory(path))
            {
                using var client = factory.CreateClient();
                var application = factory.Services.GetRequiredService<ServerApplication>();
                Assert.Equal(3, application.Runtime.Server.ThreadCount);
                using var response = await HttpApiTests.Post(client,
                    """{"gameId":"demo","command":"CreateItem","args":{"kind":"Ship","id":"s","properties":{"Fuel":{"type":"int","value":5},"Owner":{"type":"string","value":"p"}}}}""",
                    AuthenticationTests.Token("p", "demo"));
                Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
                await ((ManagedGameHost)application.Runtime.Find("demo")).Enqueue(() =>
                    Assert.Equal(5, Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get("s").GetProperty("Fuel")));
            }
            Assert.True(File.Exists(path));
            await using (var restored = new ConfiguredFactory(path))
            {
                using var client = restored.CreateClient();
                await ((ManagedGameHost)restored.Services.GetRequiredService<ServerApplication>().Runtime.Find("demo")).Enqueue(() =>
                    Assert.Equal("p", Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get("s").GetProperty("Owner")));
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    public sealed class ConfiguredFactory(string path) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:JwtSecret"] = AuthenticationTests.Secret,
                ["Server:Players:p:Commands:0"] = "CreateItem",
                ["Server:Players:p:Items:0"] = "s",
                ["Server:SnapshotPath"] = path,
                ["Server:ThreadCount"] = "3"
            }));
    }
}
