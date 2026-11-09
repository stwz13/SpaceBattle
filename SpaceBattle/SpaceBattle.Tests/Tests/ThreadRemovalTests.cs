using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ThreadRemovalTests
{
    [Fact]
    public async Task Removed_Thread_Finishes_And_All_Games_Continue_On_Their_New_Owners()
    {
        await using var fixture = new RuntimeFixture(3);
        ThreadRebalanceTests.Configure(fixture);
        using (fixture.Scope.Enter()) new RegisterIoCDependencyThreadRemoval().Execute();
        var games = Enumerable.Range(0, 20).Select(i => fixture.Runtime.Start($"g-{i}")).ToArray();
        var removed = ((Server)fixture.Runtime.Server).Threads[1];
        foreach (var game in games) game.Send(RuntimeFixture.Message(game.Id, 1));
        await fixture.Resolve<Task>("Server.RemoveThread", removed.Id).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(removed.Completion!.IsCompletedSuccessfully);
        Assert.Equal(2, fixture.Runtime.Server.ThreadCount);
        foreach (var game in games)
        {
            Assert.NotSame(removed.Thread, game.Thread);
            game.Send(RuntimeFixture.Message(game.Id, 2));
        }
        await Task.WhenAll(games.Select(game => game.Enqueue(() => { })));
        Assert.All(games, game => Assert.Equal(new[] { 1, 2 }, fixture.Seen.Where(entry => entry.Game == game.Id).Select(entry => entry.Value)));
    }

    [Fact]
    public async Task Last_Thread_And_Unknown_Thread_Cannot_Be_Removed()
    {
        await using var fixture = new RuntimeFixture(2);
        ThreadRebalanceTests.Configure(fixture);
        using (fixture.Scope.Enter()) new RegisterIoCDependencyThreadRemoval().Execute();
        var server = (Server)fixture.Runtime.Server;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Runtime.RemoveThread("missing"));
        await fixture.Runtime.RemoveThread(server.Threads[0].Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runtime.RemoveThread(server.Threads[0].Id));
        Assert.Equal(1, server.ThreadCount);
    }
}
