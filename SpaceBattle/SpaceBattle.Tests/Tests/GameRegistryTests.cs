using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameRegistryTests
{
    private sealed class Host : IGameHost
    {
        public void Send(IMessage message)
        {
        }
    }

    private readonly GameRegistry _registry = new();

    [Fact]
    public void Added_Host_Is_Found_By_Game_Identifier()
    {
        var host = new Host();
        _registry.Add("A", host);
        _registry.Add("B", new Host());

        Assert.Same(host, _registry.Find("A"));
    }

    [Fact]
    public void Unknown_Game_Is_Not_Found()
    {
        Assert.Throws<KeyNotFoundException>(() => _registry.Find("missing"));
    }

    [Fact]
    public void Game_Cannot_Be_Registered_Twice()
    {
        var first = new Host();
        _registry.Add("A", first);

        Assert.Throws<ArgumentException>(() => _registry.Add("A", new Host()));
        Assert.Same(first, _registry.Find("A"));
    }

    [Fact]
    public void Removed_Game_Is_Gone_And_Can_Be_Registered_Again()
    {
        _registry.Add("A", new Host());

        Assert.True(_registry.Remove("A"));
        Assert.False(_registry.Remove("A"));
        Assert.Throws<KeyNotFoundException>(() => _registry.Find("A"));
        var again = new Host();
        _registry.Add("A", again);
        Assert.Same(again, _registry.Find("A"));
    }

    [Fact]
    public void Registry_Can_Be_Used_From_Many_Threads()
    {
        var hosts = Enumerable.Range(0, 200).ToDictionary(index => $"G{index}", _ => new Host());

        Parallel.ForEach(hosts, pair => _registry.Add(pair.Key, pair.Value));

        Assert.All(hosts, pair => Assert.Same(pair.Value, _registry.Find(pair.Key)));
    }

    [Fact]
    public void Arguments_Are_Validated()
    {
        Assert.Throws<ArgumentException>(() => _registry.Add(" ", new Host()));
        Assert.Throws<ArgumentNullException>(() => _registry.Add("A", null!));
        Assert.Throws<ArgumentNullException>(() => _registry.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => _registry.Find(null!));
    }
}
