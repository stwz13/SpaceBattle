using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ManagedGameValidationTests
{
    public static IEnumerable<object[]> InvalidRegistrations()
    {
        var keys = new[] { GameDependencies.Create, GameDependencies.Start,
            "Games.StartRestored", "Server.SnapshotAndStop", "Server.Restore", "Commands.Server.SnapshotAndStop" };
        foreach (var key in keys)
            foreach (var arguments in new object[][] { [], [42], [null!], [42, 42] })
                yield return [key, arguments];
    }

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task Registrations_Reject_Missing_Wrong_And_Extra_Arguments(string key, object[] arguments)
    {
        await using var fixture = new RuntimeFixture();
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>(key, arguments));
        Assert.Empty(fixture.Runtime.Games);
    }

    [Fact]
    public async Task Start_Accepts_Id_And_Prepared_Game_Without_Replacing_Existing_Host()
    {
        await using var fixture = new RuntimeFixture();
        var first = fixture.Resolve<ManagedGameHost>(GameDependencies.Start, "first");
        var prepared = fixture.Resolve<IGame>(GameDependencies.Create, "second");
        var second = fixture.Resolve<ManagedGameHost>(GameDependencies.Start, prepared);
        Assert.Throws<ArgumentException>(() => fixture.Runtime.Start("first"));
        Assert.Same(first, fixture.Runtime.Find("first"));
        Assert.Same(second, fixture.Runtime.Find("second"));
        first.Send(RuntimeFixture.Message("first", 1));
        second.Send(RuntimeFixture.Message("second", 2));
        await Task.WhenAll(first.Enqueue(() => { }), second.Enqueue(() => { }));
        Assert.Equal(new[] { 1, 2 }, fixture.Seen.Select(entry => entry.Value).Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_Rejects_Duplicate_Ids_Without_Publishing_Partial_Games(bool alreadyRunning)
    {
        await using var fixture = new RuntimeFixture();
        var store = fixture.Resolve<ISnapshotStore>("Snapshots.Memory");
        const string snapshot = "{\"gameId\":\"same\",\"items\":[],\"commands\":[]}";
        store.Save("first", snapshot);
        IManagedGameHost? original = null;
        if (alreadyRunning) original = fixture.Runtime.Start("same");
        else store.Save("second", snapshot);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Runtime.Restore(store));
        if (alreadyRunning) Assert.Same(original, Assert.Single(fixture.Runtime.Games));
        else Assert.Empty(fixture.Runtime.Games);
        // Rejection releases the lifecycle lock, so unrelated games can still start.
        Assert.Equal("next", fixture.Runtime.Start("next").Id);
    }

    [Fact]
    public async Task Stopped_Server_And_Host_Reject_Work_And_Wrong_Game_Does_Not_Reach_Queue()
    {
        await using var fixture = new RuntimeFixture();
        var host = fixture.Runtime.Start("a");
        Assert.Throws<MessageFormatException>(() => host.Send(RuntimeFixture.Message("b", 1)));
        Assert.Throws<ArgumentNullException>(() => host.Send(null!));
        await fixture.Runtime.SnapshotAndStop(fixture.Resolve<ISnapshotStore>("Snapshots.Memory"));
        Assert.Equal(GameHostState.Stopped, host.State);
        Assert.Throws<InvalidOperationException>(() => fixture.Runtime.Start("later"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Enqueue(() => { }));
        Assert.Throws<ServerOverloadedException>(() => host.Send(RuntimeFixture.Message("a", 2)));
        Assert.Empty(fixture.Seen);
    }

    [Fact]
    public async Task Original_Restored_Game_Registration_Runs_Preloaded_Queue_And_Validates_Input()
    {
        await using var fixture = new RuntimeFixture();
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (fixture.Scope.Enter())
        using (new GameScope(Ioc.Resolve<object>("IoC.Scope.Create")).Enter())
        {
            new RegisterIoCDependencyMessages().Execute();
            var game = fixture.Resolve<IGame>(GameDependencies.Create, "restored");
            game.Post(new CallbackCommand(() => executed.SetResult()));
            var host = Ioc.Resolve<IGameHost>("Games.StartRestored", game);
            Assert.Same(host, Ioc.Resolve<IGameRegistry>(GameDependencies.Registry).Find("restored"));
            Assert.Throws<ArgumentException>(() => Ioc.Resolve<object>("Games.StartRestored"));
            Assert.Throws<ArgumentException>(() => Ioc.Resolve<object>("Games.StartRestored", 1));
        }
        await executed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
