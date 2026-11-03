using System.Collections.Concurrent;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class RuntimeAbstractionTests
{
    [Fact]
    public async Task Runtime_Starts_And_Snapshots_Custom_Game_Host_And_Replacement_Store()
    {
        var server = new Mock<IServer>();
        server.SetupGet(value => value.IsRunning).Returns(true);
        server.Setup(value => value.SoftStop()).Returns(Task.CompletedTask);
        var game = new Mock<IManagedGame>();
        game.SetupGet(value => value.Id).Returns("custom");
        var host = new Mock<IManagedGameHost>();
        host.SetupGet(value => value.Id).Returns("custom");
        host.SetupGet(value => value.State).Returns(GameHostState.Running);
        host.Setup(value => value.Wake()).Returns(Task.CompletedTask);
        host.Setup(value => value.PauseAndSnapshot()).ReturnsAsync("custom snapshot");
        var store = new Mock<ISnapshotReplacementStore>();
        IGameRuntime runtime = new GameRuntime(server.Object, new ConcurrentDictionary<string, IGameHost>(),
            new SemaphoreSlim(1, 1), _ => game.Object, _ => game.Object, (_, _) => host.Object);
        Assert.Same(host.Object, runtime.Start("custom"));
        Assert.Same(host.Object, Assert.Single(runtime.Games));
        var snapshots = await runtime.SnapshotAndStop(store.Object);
        Assert.Equal("custom snapshot", snapshots["custom"]);
        store.Verify(value => value.ReplaceAll(It.Is<IReadOnlyDictionary<string, string>>(items => items["custom"] == "custom snapshot")), Times.Once);
        host.Verify(value => value.Wake(), Times.Once);
        host.Verify(value => value.MarkStopped(), Times.Once);
        server.Verify(value => value.SoftStop(), Times.Once);
    }

    [Fact]
    public async Task Snapshot_Command_Uses_Only_Snapshot_Contract_And_Preserves_Completion()
    {
        var snapshots = new Mock<IServerSnapshots>();
        var store = new Mock<ISnapshotStore>();
        var pending = new TaskCompletionSource<IReadOnlyDictionary<string, string>>();
        snapshots.Setup(value => value.SnapshotAndStop(store.Object)).Returns(pending.Task);
        var command = new SnapshotAndStopCommand(snapshots.Object, store.Object);
        command.Execute();
        Assert.Same(pending.Task, command.Completion);
        Assert.Throws<InvalidOperationException>(command.Execute);
        pending.SetResult(new Dictionary<string, string> { ["a"] = "snapshot" });
        Assert.Equal("snapshot", (await command.Completion!)["a"]);
        snapshots.Verify(value => value.SnapshotAndStop(store.Object), Times.Once);
    }
}
