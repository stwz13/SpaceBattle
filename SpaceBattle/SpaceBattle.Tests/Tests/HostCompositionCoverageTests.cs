using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class HostCompositionCoverageTests
{
    [Fact]
    public void Missing_Server_Section_Uses_Default_Options_And_Requires_Explicit_Secret()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        HostComposition.Configure(services, new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ServerOptions>();
        Assert.Equal(2, options.ThreadCount);
        Assert.Empty(options.JwtSecret);
        Assert.Throws<ArgumentException>(() => provider.GetRequiredService<ServerApplication>());
    }

    [Fact]
    public async Task Host_Uses_Registered_Remote_And_Rejects_Unknown_Destination()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await using var target = new RuntimeFixture();
            GameTransferTests.Configure(target);
            await using var configured = new ServerHostTests.ConfiguredFactory(path);
            await using var factory = configured.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton<IReadOnlyDictionary<string, IRemoteServer>>(
                    new Dictionary<string, IRemoteServer> { ["target"] = target.Runtime })));
            using var client = factory.CreateClient();
            var application = factory.Services.GetRequiredService<ServerApplication>();
            var host = Assert.IsType<ManagedGameHost>(application.Runtime.Find("demo"));
            var token = AuthenticationTests.Token("p", "demo", transfer: true);
            using var unknown = await HttpApiTests.Post(client, "{\"destination\":\"unknown\"}", token, "/games/demo/transfer");
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            Assert.Equal(GameHostState.Running, host.State);
            using var moved = await HttpApiTests.Post(client, "{\"destination\":\"target\"}", token, "/games/demo/transfer");
            Assert.Equal(HttpStatusCode.NoContent, moved.StatusCode);
            Assert.Equal(GameHostState.Moved, host.State);
            Assert.Equal(GameHostState.Running, Assert.IsType<ManagedGameHost>(target.Runtime.Find("demo")).State);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Fifo_Composition_Stops_Without_Snapshot_And_Stop_Before_Start_Is_Harmless()
    {
        var options = new ServerOptions { JwtSecret = AuthenticationTests.Secret, PriorityQueues = false, Games = ["a"] };
        var store = new MemorySnapshotStore(new System.Collections.Concurrent.ConcurrentDictionary<string, string>());
        var application = ServerComposition.Create(options, store);
        await application.Start();
        var host = Assert.Single(application.Runtime.Games);
        Assert.Equal("a", host.Id);
        await application.Stop();
        await application.Stop();
        Assert.Equal(ServerState.Stopped, application.Runtime.Server.State);
        Assert.Empty(application.Store.GameIds);
        var unstarted = ServerComposition.Create(options, store);
        await unstarted.Stop();
        Assert.Equal(ServerState.Created, unstarted.Runtime.Server.State);
    }

    [Fact]
    public void Application_Log_Preserves_Exception_Game_Action_And_Attempt()
    {
        var logger = new Mock<ILogger<ApplicationLog>>();
        var error = new IOException("write failed");
        new ApplicationLog(logger.Object).Write("game-7", error, ErrorAction.Skip, 2);
        logger.Verify(value => value.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("game-7")
                && state.ToString()!.Contains("Skip") && state.ToString()!.Contains("2")),
            error, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
