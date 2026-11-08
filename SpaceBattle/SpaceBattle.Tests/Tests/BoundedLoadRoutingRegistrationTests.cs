using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class BoundedLoadRoutingRegistrationTests
{
    [Fact]
    public void Tolerance_And_Probes_Are_Validated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyBoundedLoadRouting(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyBoundedLoadRouting(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegisterIoCDependencyBoundedLoadRouting(0, 0));
    }

    [Fact]
    public async Task Server_Spreads_Games_Evenly_When_Bounded_Load_Is_Enabled()
    {
        var options = new ServerOptions
        {
            JwtSecret = "0123456789abcdef0123456789abcdef-test-secret", PriorityQueues = false, ThreadCount = 3,
            QuantumMilliseconds = 0, LoadTolerance = 0
        };
        var application = ServerComposition.Create(options,
            new MemorySnapshotStore(new System.Collections.Concurrent.ConcurrentDictionary<string, string>()));
        await application.Start();
        try
        {
            Enumerable.Range(0, 30).ToList().ForEach(index => application.Runtime.Start($"game-{index}"));

            var perThread = application.Runtime.Games.GroupBy(game => game.Thread).Select(group => group.Count()).ToArray();
            Assert.Equal(3, perThread.Length);
            Assert.All(perThread, count => Assert.Equal(10, count));
        }
        finally
        {
            await application.Stop();
        }
    }

    [Fact]
    public async Task Rebalancing_Uses_The_Bounded_Router()
    {
        var options = new ServerOptions
        {
            JwtSecret = "0123456789abcdef0123456789abcdef-test-secret", PriorityQueues = false, ThreadCount = 1,
            QuantumMilliseconds = 0, LoadTolerance = 0
        };
        var application = ServerComposition.Create(options,
            new MemorySnapshotStore(new System.Collections.Concurrent.ConcurrentDictionary<string, string>()));
        await application.Start();
        try
        {
            Enumerable.Range(0, 12).ToList().ForEach(index => application.Runtime.Start($"game-{index}"));

            await application.Runtime.AddThread();

            var perThread = application.Runtime.Games.GroupBy(game => game.Thread).Select(group => group.Count()).ToArray();
            Assert.Equal(2, perThread.Length);
            Assert.All(perThread, count => Assert.InRange(count, 5, 7));
        }
        finally
        {
            await application.Stop();
        }
    }
}
