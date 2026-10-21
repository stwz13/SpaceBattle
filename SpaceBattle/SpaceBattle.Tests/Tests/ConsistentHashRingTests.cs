using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ConsistentHashRingTests
{
    private static ConsistentHashRing<StubServerThread> Ring(int replicas = 128) => new(new Sha256HashFunction(), replicas);

    private static StubServerThread Node(string name) => new(name);

    private static ConsistentHashRing<StubServerThread> Filled(int replicas, params string[] ids)
    {
        var ring = Ring(replicas);
        foreach (var id in ids)
        {
            ring.Add(id, Node(id));
        }
        return ring;
    }

    private static IEnumerable<string> Keys(int count) => Enumerable.Range(0, count).Select(index => $"Battle-{index}");

    private static ConsistentHashRing<StubServerThread> FakeRing()
    {
        var hash = new FakeHashFunction(new Dictionary<string, ulong>
        {
            ["A#0"] = 100,
            ["B#0"] = 200,
            ["C#0"] = 300,
            ["before"] = 50,
            ["exact"] = 200,
            ["between"] = 201,
            ["last"] = 300,
            ["after"] = 301,
        });
        var ring = new ConsistentHashRing<StubServerThread>(hash, 1);
        ring.Add("B", Node("B"));
        ring.Add("C", Node("C"));
        ring.Add("A", Node("A"));
        return ring;
    }

    [Theory]
    [InlineData("before", "A")]
    [InlineData("exact", "B")]
    [InlineData("between", "C")]
    [InlineData("last", "C")]
    [InlineData("after", "A")]
    public void Key_Goes_To_The_First_Node_At_Or_After_Its_Position_And_Wraps_Around(string key, string expected)
    {
        Assert.Equal(expected, FakeRing().Find(key).Name);
    }

    [Fact]
    public void Single_Node_Gets_Every_Key()
    {
        var ring = Filled(16, "only");

        Assert.All(Keys(200), key => Assert.Equal("only", ring.Find(key).Name));
    }

    [Fact]
    public void Same_Key_Always_Goes_To_The_Same_Node()
    {
        var ring = Filled(64, "A", "B", "C");

        Assert.All(Keys(200), key => Assert.Same(ring.Find(key), ring.Find(key)));
    }

    [Fact]
    public void Order_Of_Adding_Nodes_Does_Not_Matter()
    {
        var first = Filled(64, "A", "B", "C", "D");
        var second = Filled(64, "D", "B", "A", "C");

        Assert.All(Keys(500), key => Assert.Equal(first.Find(key).Name, second.Find(key).Name));
    }

    [Fact]
    public void Keys_Are_Spread_Over_All_Nodes()
    {
        var ring = Filled(128, "A", "B", "C");

        var shares = Keys(3000).GroupBy(key => ring.Find(key).Name).ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(3, shares.Count);
        Assert.All(shares.Values, share => Assert.True(share > 600 && share < 1500, $"Uneven share {share}"));
    }

    [Fact]
    public void Adding_A_Node_Only_Moves_Keys_To_That_Node()
    {
        var ring = Filled(64, "A", "B", "C", "D");
        var before = Keys(2000).ToDictionary(key => key, key => ring.Find(key).Name);

        ring.Add("E", Node("E"));

        var after = Keys(2000).ToDictionary(key => key, key => ring.Find(key).Name);
        var moved = before.Keys.Where(key => before[key] != after[key]).ToArray();
        Assert.All(moved, key => Assert.Equal("E", after[key]));
        Assert.True(moved.Length > 0);
        Assert.True(moved.Length < 1000, $"Too many keys moved: {moved.Length}");
    }

    [Fact]
    public void Removing_A_Node_Only_Moves_The_Keys_It_Owned()
    {
        var ring = Filled(64, "A", "B", "C", "D");
        var before = Keys(2000).ToDictionary(key => key, key => ring.Find(key).Name);

        Assert.True(ring.Remove("B"));

        var after = Keys(2000).ToDictionary(key => key, key => ring.Find(key).Name);
        Assert.All(before.Keys.Where(key => before[key] != "B"), key => Assert.Equal(before[key], after[key]));
        Assert.All(before.Keys.Where(key => before[key] == "B"), key => Assert.NotEqual("B", after[key]));
        Assert.Equal(3, ring.Count);
    }

    [Fact]
    public void Removed_Node_Can_Be_Added_Again_And_Gets_Its_Keys_Back()
    {
        var ring = Filled(64, "A", "B", "C");
        var before = Keys(500).ToDictionary(key => key, key => ring.Find(key).Name);

        ring.Remove("B");
        ring.Add("B", Node("B"));

        Assert.All(before, pair => Assert.Equal(pair.Value, ring.Find(pair.Key).Name));
    }

    [Fact]
    public void Removing_An_Unknown_Node_Does_Nothing()
    {
        var ring = Filled(8, "A");

        Assert.False(ring.Remove("missing"));
        Assert.Equal(1, ring.Count);
    }

    [Fact]
    public void Empty_Ring_Cannot_Find_Anything()
    {
        var ring = Ring();
        Assert.Throws<InvalidOperationException>(() => ring.Find("Battle"));

        ring.Add("A", Node("A"));
        ring.Remove("A");

        Assert.Equal(0, ring.Count);
        Assert.Throws<InvalidOperationException>(() => ring.Find("Battle"));
    }

    [Fact]
    public void Node_Returned_Is_The_One_That_Was_Added()
    {
        var node = Node("A");
        var ring = Ring(4);
        ring.Add("A", node);

        Assert.Same(node, ring.Find("anything"));
    }

    [Fact]
    public void Same_Node_Cannot_Be_Added_Twice()
    {
        var ring = Filled(4, "A");

        Assert.Throws<ArgumentException>(() => ring.Add("A", Node("other")));
        Assert.Equal(1, ring.Count);
    }

    [Fact]
    public void Nodes_With_Equal_Positions_Are_Resolved_The_Same_Way_Whatever_The_Order()
    {
        var hash = new FakeHashFunction(new Dictionary<string, ulong>
        {
            ["A#0"] = 100,
            ["B#0"] = 100,
            ["key"] = 100,
        });
        var first = new ConsistentHashRing<StubServerThread>(hash, 1);
        first.Add("A", Node("A"));
        first.Add("B", Node("B"));
        var second = new ConsistentHashRing<StubServerThread>(hash, 1);
        second.Add("B", Node("B"));
        second.Add("A", Node("A"));

        Assert.Equal("A", first.Find("key").Name);
        Assert.Equal("A", second.Find("key").Name);
    }

    [Fact]
    public async Task Ring_Can_Be_Used_While_Nodes_Come_And_Go()
    {
        var ring = Filled(32, "Base");
        var stop = false;
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            foreach (var key in Keys(4000))
            {
                try
                {
                    Assert.NotNull(ring.Find(key));
                }
                catch (Exception exception)
                {
                    failures.Enqueue(exception);
                }
            }
        })).ToArray();
        var writer = Task.Run(() =>
        {
            foreach (var _ in Enumerable.Range(0, 200).TakeWhile(_ => !Volatile.Read(ref stop)))
            {
                ring.Add("Extra", Node("Extra"));
                ring.Remove("Extra");
            }
        });

        await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(30));
        Volatile.Write(ref stop, true);
        await writer.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Empty(failures.ToArray());
        Assert.Equal(1, ring.Count);
    }

    [Fact]
    public void Invalid_Arguments_Are_Rejected()
    {
        var hash = new Sha256HashFunction();
        var ring = Ring(4);

        Assert.Throws<ArgumentNullException>(() => new ConsistentHashRing<StubServerThread>(null!, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsistentHashRing<StubServerThread>(hash, 0));
        Assert.Throws<ArgumentNullException>(() => ring.Add(null!, Node("A")));
        Assert.Throws<ArgumentException>(() => ring.Add(" ", Node("A")));
        Assert.Throws<ArgumentNullException>(() => ring.Add("A", null!));
        Assert.Throws<ArgumentNullException>(() => ring.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => ring.Find(null!));
    }
}
