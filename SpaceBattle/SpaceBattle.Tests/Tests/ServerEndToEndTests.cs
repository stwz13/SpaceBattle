using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ServerEndToEndTests
{
    [Fact]
    [Trait("Category", "EndToEnd")]
    public async Task Two_Players_Overload_Transfer_Snapshot_Restore_And_Stop()
    {
        var sourceStore = new MemorySnapshotStore(new ConcurrentDictionary<string, string>());
        var targetStore = new MemorySnapshotStore(new ConcurrentDictionary<string, string>());
        await using var target = new ApplicationFactory([], targetStore);
        using var targetClient = target.CreateClient();
        var remote = new GameTransferTests.GatedRemote(target.Application.Runtime);
        await using var source = new ApplicationFactory(["a", "b"], sourceStore,
            new Dictionary<string, IRemoteServer> { ["target"] = remote });
        using var sourceClient = source.CreateClient();
        var firstToken = AuthenticationTests.Token("p1", "a", transfer: true);
        var secondToken = AuthenticationTests.Token("p2", "b");
        await Create(sourceClient, "a", "p1", firstToken);
        await Barrier(source.Application, "a");
        await Create(sourceClient, "b", "p2", secondToken);
        await Barrier(source.Application, "b");

        var first = (ManagedGameHost)source.Application.Runtime.Find("a");
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = first.Execute(() => { entered.SetResult(); release.Wait(TimeSpan.FromSeconds(10)); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var accepted = await HttpApiTests.Post(sourceClient, Increment("a", 1), firstToken);
            using var overloaded = await HttpApiTests.Post(sourceClient, Increment("a", 1000), firstToken);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, overloaded.StatusCode);
            Assert.NotNull(overloaded.Headers.RetryAfter);
        }
        finally { release.Set(); }
        await blocked;
        await Barrier(source.Application, "a");

        var moving = HttpApiTests.Post(sourceClient, "{\"destination\":\"target\"}", firstToken, "/games/a/transfer");
        await remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using (var buffered = await HttpApiTests.Post(sourceClient, Increment("a", 2), firstToken))
            Assert.Equal(HttpStatusCode.Accepted, buffered.StatusCode);
        remote.Release.SetResult();
        using (var moved = await moving.WaitAsync(TimeSpan.FromSeconds(10))) Assert.Equal(HttpStatusCode.NoContent, moved.StatusCode);
        // Admission completes before the target's activation command drains its bounded inbox.
        await Barrier(target.Application, "a");
        using (var proxied = await HttpApiTests.Post(sourceClient, Increment("a", 3), firstToken))
            Assert.Equal(HttpStatusCode.Accepted, proxied.StatusCode);
        await Barrier(target.Application, "a");
        using (var otherPlayer = await HttpApiTests.Post(sourceClient, Increment("b", 4), secondToken))
            Assert.Equal(HttpStatusCode.Accepted, otherPlayer.StatusCode);
        await Barrier(source.Application, "b");
        await AssertFuel(target.Application, "a", 6, "p1");
        await AssertFuel(source.Application, "b", 4, "p2");

        await Task.WhenAll(source.Application.Runtime.SnapshotAndStop(sourceStore), target.Application.Runtime.SnapshotAndStop(targetStore));
        Assert.Equal(new[] { "b" }, sourceStore.GameIds);
        Assert.Equal(new[] { "a" }, targetStore.GameIds);
        var combined = new MemorySnapshotStore(new ConcurrentDictionary<string, string>(sourceStore.GameIds
            .Select(id => KeyValuePair.Create(id, sourceStore.Load(id))).Concat(targetStore.GameIds.Select(id => KeyValuePair.Create(id, targetStore.Load(id))))));
        await using var restored = new ApplicationFactory([], combined, restore: true);
        using var restoredClient = restored.CreateClient();
        await AssertFuel(restored.Application, "a", 6, "p1");
        await AssertFuel(restored.Application, "b", 4, "p2");
        using (var response = await HttpApiTests.Post(restoredClient, Increment("a", 5), firstToken)) Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await Barrier(restored.Application, "a");
        using (var response = await HttpApiTests.Post(restoredClient, Increment("b", 6), secondToken)) Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await AssertFuel(restored.Application, "a", 11, "p1");
        await AssertFuel(restored.Application, "b", 10, "p2");
        await restored.Application.Stop().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(ServerState.Stopped, restored.Application.Runtime.Server.State);
        Assert.Empty(source.Log.Entries);
        Assert.Empty(target.Log.Entries);
        Assert.Empty(restored.Log.Entries);
    }

    private static string Increment(string game, int amount) => JsonSerializer.Serialize(new { gameId = game, command = "AddFuel", args = new { id = "ship", amount } });
    private static async Task Create(HttpClient client, string game, string player, string token)
    {
        using var response = await HttpApiTests.Post(client, JsonSerializer.Serialize(new
        {
            gameId = game, command = "CreateItem", args = new
            {
                id = "ship", kind = "Ship", properties = new
                {
                    Fuel = new { type = "int", value = 0 }, Owner = new { type = "string", value = player }
                }
            }
        }), token);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
    private static Task Barrier(ServerApplication app, string game) => ((ManagedGameHost)app.Runtime.Find(game)).Enqueue(() => { });
    private static Task AssertFuel(ServerApplication app, string game, int value, string owner) => ((ManagedGameHost)app.Runtime.Find(game)).Enqueue(() =>
    {
        var item = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get("ship");
        Assert.Equal(value, item.GetProperty("Fuel"));
        Assert.Equal(owner, item.GetProperty("Owner"));
    });

    public sealed class ApplicationFactory(string[] games, ISnapshotStore store,
        IReadOnlyDictionary<string, IRemoteServer>? targets = null, bool restore = false) : WebApplicationFactory<Program>
    {
        public MemoryLog Log { get; } = new(new ConcurrentQueue<(string, Exception, ErrorAction, int)>());
        public ServerApplication Application => Services.GetRequiredService<ServerApplication>();
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            var options = new ServerOptions
            {
                ThreadCount = 1, InboxCapacity = 1, QuantumMilliseconds = 0, Games = games,
                AllowedCommands = ["CreateItem", "AddFuel"], ItemKinds = new() { ["Ship"] = ["Fuel", "Owner"] },
                JwtSecret = AuthenticationTests.Secret, SnapshotOnStop = true, RestoreOnStart = restore,
                CommandTargets = new() { ["CreateItem"] = "id", ["AddFuel"] = "id" },
                Players = new()
                {
                    ["p1"] = new() { Commands = ["CreateItem", "AddFuel"], Items = ["ship"] },
                    ["p2"] = new() { Commands = ["CreateItem", "AddFuel"], Items = ["ship"] }
                }
            };
            services.AddSingleton(options);
            services.AddSingleton(_ => ServerComposition.Create(options, store, () =>
                RuntimeFixture.Register("Messages.Command.AddFuel", args => new CallbackCommand(() =>
                {
                    var payload = ((IMessage)args[0]).Args;
                    var item = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get(payload.GetProperty("id").GetString()!);
                    item.SetProperty("Fuel", (int)item.GetProperty("Fuel") + payload.GetProperty("amount").GetInt32());
                })), Log));
            services.AddSingleton(targets ?? new Dictionary<string, IRemoteServer>());
        });
    }
}
