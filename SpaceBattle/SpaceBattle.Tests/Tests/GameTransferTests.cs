using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameTransferTests
{
    public static void Configure(RuntimeFixture fixture)
    {
        using var entered = fixture.Scope.Enter();
        new RegisterIoCDependencyGameTransfer(100).Execute();
    }

    [Fact]
    public async Task Transfer_Preserves_Order_And_Old_Host_Forwards_Later_Messages()
    {
        await using var source = new RuntimeFixture();
        await using var target = new RuntimeFixture();
        Configure(source); Configure(target);
        var host = source.Runtime.Start("game");
        foreach (var i in Enumerable.Range(0, 10)) host.Send(RuntimeFixture.Message("game", i));
        var remote = new GatedRemote(target.Runtime);
        var transfer = source.Runtime.Transfer("game", remote);
        await remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var i in Enumerable.Range(10, 10)) host.Send(RuntimeFixture.Message("game", i));
        Assert.Equal(GameHostState.Moving, host.State);
        remote.Release.SetResult();
        await transfer.WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var i in Enumerable.Range(20, 10)) host.Send(RuntimeFixture.Message("game", i));
        await ((ManagedGameHost)target.Runtime.Find("game")).Enqueue(() => { });
        Assert.Equal(Enumerable.Range(0, 30), source.Seen.Concat(target.Seen).Select(entry => entry.Value));
        Assert.Equal(GameHostState.Moved, host.State);
        Assert.Same(host, source.Runtime.Find("game"));
    }

    [Fact]
    public async Task Failed_Receive_Resumes_Source_And_Executes_Buffer_Once()
    {
        await using var source = new RuntimeFixture();
        Configure(source);
        var host = source.Runtime.Start("game");
        var remote = new GatedRemote(null);
        var transfer = source.Runtime.Transfer("game", remote);
        await remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var i in Enumerable.Range(0, 10)) host.Send(RuntimeFixture.Message("game", i));
        remote.Release.SetResult();
        await Assert.ThrowsAsync<IOException>(() => transfer);
        host.Send(RuntimeFixture.Message("game", 10));
        await host.Enqueue(() => { });
        Assert.Equal(Enumerable.Range(0, 11), source.Seen.Select(entry => entry.Value));
        Assert.Equal(GameHostState.Running, host.State);
    }

    [Fact]
    public async Task Duplicate_Target_Rejects_Transfer_Without_Damaging_Either_Game()
    {
        await using var source = new RuntimeFixture();
        await using var target = new RuntimeFixture();
        Configure(source); Configure(target);
        var first = source.Runtime.Start("game");
        var second = target.Runtime.Start("game");
        await Assert.ThrowsAsync<ArgumentException>(() => source.Runtime.Transfer("game", target.Runtime));
        first.Send(RuntimeFixture.Message("game", 1)); second.Send(RuntimeFixture.Message("game", 2));
        await Task.WhenAll(first.Enqueue(() => { }), second.Enqueue(() => { }));
        Assert.Equal(1, Assert.Single(source.Seen).Value);
        Assert.Equal(2, Assert.Single(target.Seen).Value);
        Assert.Throws<ArgumentException>(() => source.Resolve<object>("Games.Transfer", "game"));
    }

    public sealed class GatedRemote(IRemoteServer? target) : IRemoteServer
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Send(IMessage message) => target!.Send(message);
        public async Task<IGameArrival> Receive(string snapshot)
        {
            Entered.SetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (target is null) throw new IOException("Network unavailable.");
            return await target.Receive(snapshot);
        }
    }
}
