using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class FnvHashFunctionTests
{
    private readonly FnvHashFunction _hash = new();

    [Theory]
    [InlineData("", 0xEFD01F60BA992926UL)]
    [InlineData("a", 0x86FC2D0407502DE5UL)]
    [InlineData("abc", 0x6DBBB2667D84FE52UL)]
    [InlineData("Battle-42", 0x3BE28E3CC473C960UL)]
    [InlineData("игра", 0xBB55AA736A2F5509UL)]
    public void Hash_Matches_The_Reference_Values(string key, ulong expected)
    {
        Assert.Equal(expected, _hash.Hash(key));
    }

    [Fact]
    public void Hash_Does_Not_Depend_On_The_Instance_Or_The_Run()
    {
        Assert.Equal(_hash.Hash("Battle-42"), new FnvHashFunction().Hash("Battle-42"));
        Assert.NotEqual(_hash.Hash("Battle-42"), _hash.Hash("Battle-43"));
    }

    [Fact]
    public void Hash_Rejects_A_Missing_Key()
    {
        Assert.Throws<ArgumentNullException>(() => _hash.Hash(null!));
    }

    [Fact]
    public void Very_Long_Keys_Are_Hashed_Without_Exhausting_The_Stack()
    {
        var key = new string('x', 1_000_000);

        Assert.Equal(_hash.Hash(key), _hash.Hash(key));
    }

    [Fact]
    public void Games_Are_Spread_Evenly_Between_Threads_On_The_Ring()
    {
        var ring = new ConsistentHashRing<StubServerThread>(_hash, 100);
        foreach (var name in new[] { "Thread-0", "Thread-1", "Thread-2", "Thread-3" })
        {
            ring.Add(name, new StubServerThread(name));
        }

        var shares = Enumerable.Range(0, 10000)
            .GroupBy(index => ring.Find($"Battle-{index}").Name)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(4, shares.Count);
        Assert.All(shares.Values, share => Assert.InRange(share, 1500, 3500));
    }
}
