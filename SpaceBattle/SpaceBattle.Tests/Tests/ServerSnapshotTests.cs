using System.Collections.Concurrent;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ServerSnapshotTests
{
    [Fact]
    public async Task Snapshot_Command_Replaces_Previous_Generation_Without_Stale_Games()
    {
        await using var fixture = new RuntimeFixture();
        fixture.Runtime.Start("current");
        var store = fixture.Resolve<ISnapshotStore>("Snapshots.Memory");
        store.Save("old", "{}");
        var command = fixture.Resolve<SnapshotAndStopCommand>("Commands.Server.SnapshotAndStop", store);
        command.Execute();
        await command.Completion!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { "current" }, store.GameIds);
        Assert.Throws<InvalidOperationException>(command.Execute);
    }

    [Fact]
    public async Task Two_Games_Preserve_Items_And_Exactly_The_Unexecuted_Tail()
    {
        await using var source = new RuntimeFixture(1);
        var games = new[] { source.Runtime.Start("a"), source.Runtime.Start("b") };
        await Task.WhenAll(games.Select(host => host.Enqueue(() =>
            Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship", "ship",
                new[] { KeyValuePair.Create("Fuel", (object)17) }.AsEnumerable()))));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = games[0].Execute(() => { entered.SetResult(); release.Wait(TimeSpan.FromSeconds(10)); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var store = source.Resolve<ISnapshotStore>("Snapshots.Memory");
        Task<IReadOnlyDictionary<string, string>> stopping;
        try
        {
            foreach (var index in Enumerable.Range(0, 20))
                foreach (var game in games) game.Send(RuntimeFixture.Message(game.Id, index));
            stopping = source.Runtime.SnapshotAndStop(store);
            Assert.Throws<ServerOverloadedException>(() => games[0].Send(RuntimeFixture.Message("a", 99)));
        }
        finally { release.Set(); }
        var snapshots = await stopping.WaitAsync(TimeSpan.FromSeconds(10));
        await blocked;
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(ServerState.Stopped, source.Runtime.Server.State);
        await using var target = new RuntimeFixture();
        await target.Resolve<Task>("Server.Restore", store).WaitAsync(TimeSpan.FromSeconds(10));
        await Task.WhenAll(target.Runtime.Games.Select(game => game.Enqueue(() =>
            Assert.Equal(17, Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get("ship").GetProperty("Fuel")))));
        foreach (var id in new[] { "a", "b" })
            Assert.Equal(Enumerable.Range(0, 20), source.Seen.Concat(target.Seen).Where(entry => entry.Game == id).Select(entry => entry.Value));
        Assert.Empty(source.Errors.Handled);
        Assert.Empty(target.Errors.Handled);
    }

    [Fact]
    public async Task Failed_Save_Resumes_Games_And_Does_Not_Stop_Server()
    {
        await using var fixture = new RuntimeFixture();
        var host = fixture.Runtime.Start("a");
        await Assert.ThrowsAsync<IOException>(() => fixture.Runtime.SnapshotAndStop(new FailingStore()));
        host.Send(RuntimeFixture.Message("a", 7));
        await host.Enqueue(() => { });
        Assert.Equal(7, Assert.Single(fixture.Seen).Value);
        Assert.Equal(GameHostState.Running, host.State);
    }

    [Fact]
    public async Task Registrations_Start_An_Already_Restored_Game_And_Validate_Arguments()
    {
        await using var fixture = new RuntimeFixture();
        var game = fixture.Resolve<IGame>(SerializationDependencies.Restore,
            "{\"gameId\":\"a\",\"items\":[],\"commands\":[{\"type\":\"message\",\"gameId\":\"a\",\"command\":\"Record\",\"args\":3}]}");
        var host = fixture.Resolve<ManagedGameHost>("Games.StartRestored", game);
        await host.Enqueue(() => { });
        Assert.Equal(3, Assert.Single(fixture.Seen).Value);
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>("Games.StartRestored", "a"));
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>("Server.SnapshotAndStop"));
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>("Server.Restore"));
    }

    private sealed class FailingStore : ISnapshotStore
    {
        public IReadOnlyCollection<string> GameIds => [];
        public void Save(string gameId, string json) => throw new IOException("disk full");
        public string Load(string gameId) => throw new KeyNotFoundException();
    }
}
