using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ThreadRebalanceTests
{
    public static void Configure(RuntimeFixture fixture)
    {
        GameTransferTests.Configure(fixture);
        using var scope = fixture.Scope.Enter();
        new RegisterIoCDependencyRebalancing().Execute();
    }

    [Fact]
    public async Task Adding_Fourth_Thread_Moves_Only_Remapped_Games_And_Keeps_Hosts_Stable()
    {
        await using var fixture = new RuntimeFixture(3);
        Configure(fixture);
        var hosts = Enumerable.Range(0, 20).Select(i => fixture.Runtime.Start($"game-{i}")).ToArray();
        var originalThreads = hosts.ToDictionary(host => host.Id, host => host.Thread);
        var originalItems = new System.Collections.Concurrent.ConcurrentDictionary<string, IGameItem>();
        await Task.WhenAll(hosts.Select(host => host.Enqueue(() => originalItems[host.Id] =
            Ioc.Resolve<IGameItem>(ItemDependencies.Create, "Ship", "s", new[] { KeyValuePair.Create("Fuel", (object)5) }.AsEnumerable()))));
        foreach (var host in hosts) host.Send(RuntimeFixture.Message(host.Id, 1));
        var added = await fixture.Resolve<Task<IServerThread>>("Server.AddThread").WaitAsync(TimeSpan.FromSeconds(10));
        var moved = hosts.Where(host => !ReferenceEquals(host.Thread, originalThreads[host.Id])).ToArray();
        Assert.NotEmpty(moved);
        Assert.True(moved.Length < hosts.Length);
        await Task.WhenAll(hosts.Select(host => host.Enqueue(() =>
        {
            var item = Ioc.Resolve<IBattleContext>(BattleContextDependencies.Context).Storage.Get("s");
            Assert.Equal(5, item.GetProperty("Fuel"));
            if (!moved.Contains(host)) Assert.Same(originalItems[host.Id], item);
        })));
        foreach (var host in hosts)
        {
            Assert.Same(host, fixture.Runtime.Find(host.Id));
            Assert.Same(fixture.Resolve<IServerThread>(RoutingDependencies.Thread, host.Id), host.Thread);
            host.Send(RuntimeFixture.Message(host.Id, 2));
        }
        await Task.WhenAll(hosts.Select(host => host.Enqueue(() => { })));
        Assert.All(moved, host => Assert.Same(added, host.Thread));
        Assert.All(hosts, host => Assert.Equal(new[] { 1, 2 }, fixture.Seen.Where(entry => entry.Game == host.Id).Select(entry => entry.Value)));
        Assert.Throws<ArgumentException>(() => fixture.Resolve<object>("Server.AddThread", 1));
    }
}
