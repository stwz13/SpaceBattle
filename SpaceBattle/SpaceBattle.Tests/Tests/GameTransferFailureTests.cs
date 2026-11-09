using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameTransferFailureTests
{
    private const string EmptyGame = "{\"gameId\":\"game\",\"items\":[],\"commands\":[]}";

    [Fact]
    public async Task Full_Transfer_Buffer_Rejects_Only_Excess_Message_And_Rollback_Preserves_Order()
    {
        await using var source = new RuntimeFixture();
        using (source.Scope.Enter()) new RegisterIoCDependencyGameTransfer(2).Execute();
        var host = source.Runtime.Start("game");
        var remote = new GameTransferTests.GatedRemote(null);
        var transferring = source.Runtime.Transfer("game", remote);
        await remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            host.Send(RuntimeFixture.Message("game", 1));
            host.Send(RuntimeFixture.Message("game", 2));
            Assert.Throws<ServerOverloadedException>(() => host.Send(RuntimeFixture.Message("game", 3)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Enqueue(() => { }));
            Assert.Throws<ServerOverloadedException>(() => source.Runtime.Start("other"));
            await Assert.ThrowsAsync<ServerOverloadedException>(() => source.Runtime.Transfer("game", remote));
            await Assert.ThrowsAsync<ServerOverloadedException>(() => source.Runtime.Receive(EmptyGame));
        }
        finally { remote.Release.TrySetResult(); }
        await Assert.ThrowsAsync<IOException>(() => transferring);
        host.Send(RuntimeFixture.Message("game", 4));
        await host.Enqueue(() => { });
        Assert.Equal(new[] { 1, 2, 4 }, source.Seen.Select(entry => entry.Value));
        Assert.Equal(GameHostState.Running, host.State);
        Assert.Empty(source.Errors.Handled);
    }

    [Fact]
    public async Task Failed_Commit_Aborts_Arrival_And_Resumes_Buffered_Source()
    {
        await using var source = new RuntimeFixture();
        GameTransferTests.Configure(source);
        var host = source.Runtime.Start("game");
        var aborted = 0;
        var arrival = new GameArrival(new object(), _ => throw new IOException("Cannot publish target."), () => aborted++);
        var remote = new Mock<IRemoteServer>();
        remote.Setup(value => value.Receive(It.IsAny<string>())).Returns(() =>
        {
            host.Send(RuntimeFixture.Message("game", 7));
            return Task.FromResult<IGameArrival>(arrival);
        });
        await Assert.ThrowsAsync<IOException>(() => source.Runtime.Transfer("game", remote.Object));
        await host.Enqueue(() => { });
        Assert.Equal(1, aborted);
        Assert.False(arrival.IsCommitted);
        Assert.Equal(7, Assert.Single(source.Seen).Value);
        Assert.Equal(GameHostState.Running, host.State);
    }

    [Fact]
    public async Task Aborted_Arrival_Is_Invisible_And_Releases_Target_For_A_New_Transfer()
    {
        await using var target = new RuntimeFixture();
        GameTransferTests.Configure(target);
        var aborted = await target.Runtime.Receive(EmptyGame);
        Assert.Throws<KeyNotFoundException>(() => target.Runtime.Find("game"));
        await Assert.ThrowsAsync<ServerOverloadedException>(() => target.Runtime.Receive(EmptyGame));
        aborted.Abort();
        aborted.Abort();
        Assert.Throws<InvalidOperationException>(() => aborted.Commit([]));
        var accepted = await target.Runtime.Receive(EmptyGame);
        accepted.Commit([RuntimeFixture.Message("game", 5)]);
        accepted.Commit([RuntimeFixture.Message("game", 99)]);
        accepted.Abort();
        var host = Assert.IsType<ManagedGameHost>(target.Runtime.Find("game"));
        await host.Enqueue(() => { });
        Assert.True(accepted.IsCommitted);
        Assert.Equal(5, Assert.Single(target.Seen).Value);
        Assert.Equal("next", target.Runtime.Start("next").Id);
    }

    [Fact]
    public async Task Invalid_Transfer_Requests_Leave_Server_Usable()
    {
        await using var source = new RuntimeFixture();
        await using var target = new RuntimeFixture();
        GameTransferTests.Configure(source);
        GameTransferTests.Configure(target);
        await Assert.ThrowsAsync<ArgumentException>(() => source.Runtime.Transfer("game", source.Runtime));
        await Assert.ThrowsAsync<ArgumentNullException>(() => source.Runtime.Transfer("game", null!));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => source.Runtime.Transfer("missing", target.Runtime));
        var host = source.Runtime.Start("game");
        await source.Resolve<Task>("Games.Transfer", "game", target.Runtime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.Runtime.Transfer("game", target.Runtime));
        host.Send(RuntimeFixture.Message("game", 8));
        await ((ManagedGameHost)target.Runtime.Find("game")).Enqueue(() => { });
        Assert.Equal(8, Assert.Single(target.Seen).Value);
        Assert.Equal(GameHostState.Moved, host.State);
    }

    [Fact]
    public async Task Stopped_Target_Rejects_Arrival_Before_Publishing_It()
    {
        await using var target = new RuntimeFixture();
        GameTransferTests.Configure(target);
        await target.Runtime.Server.SoftStop();
        await Assert.ThrowsAsync<InvalidOperationException>(() => target.Runtime.Receive(EmptyGame));
        Assert.Empty(target.Runtime.Games);
    }

    [Fact]
    public async Task Transfer_Registration_Rejects_Invalid_Destinations_And_Capacity()
    {
        await using var fixture = new RuntimeFixture();
        GameTransferTests.Configure(fixture);
        Assert.Same(fixture.Runtime, fixture.Resolve<IRemoteServer>("Server.Remote"));
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>("Games.Transfer", 1, fixture.Runtime));
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>("Games.Transfer", "game", 1));
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>("Games.Transfer", "game", null!));
        using var scope = fixture.Scope.Enter();
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyGameTransfer(0).Execute());
    }
}
