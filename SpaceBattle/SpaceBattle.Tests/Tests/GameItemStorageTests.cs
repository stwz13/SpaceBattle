using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameItemStorageTests
{
    private readonly IGameItemStorage _storage = new GameItemStorage();

    [Fact]
    public void New_Storage_Is_Empty()
    {
        Assert.Empty(_storage.GetSnapshot());
        Assert.Throws<KeyNotFoundException>(() => _storage.Get("Missing"));
        Assert.False(_storage.Remove("Missing"));
    }

    [Fact]
    public void Added_Item_Can_Be_Retrieved_And_Removed()
    {
        var item = new TestGameItem();
        _storage.Add("Ship", item);

        Assert.Same(item, _storage.Get("Ship"));
        Assert.Same(item, Assert.Single(_storage.GetSnapshot()));
        Assert.True(_storage.Remove("Ship"));
        Assert.False(_storage.Remove("Ship"));
        Assert.Throws<KeyNotFoundException>(() => _storage.Get("Ship"));
        Assert.Empty(_storage.GetSnapshot());
    }

    [Fact]
    public void Duplicate_Id_Is_Rejected_Without_Replacing_The_Item()
    {
        var item = new TestGameItem();
        _storage.Add("Ship", item);

        Assert.Throws<ArgumentException>(() => _storage.Add("Ship", new TestGameItem()));

        Assert.Same(item, _storage.Get("Ship"));
        Assert.Single(_storage.GetSnapshot());
    }

    [Fact]
    public void Ids_Are_Ordinal_And_Case_Sensitive()
    {
        var upper = new TestGameItem();
        var lower = new TestGameItem();
        var composed = new TestGameItem();
        var decomposed = new TestGameItem();
        _storage.Add("Ship", upper);
        _storage.Add("ship", lower);
        _storage.Add("\u00e9", composed);
        _storage.Add("e\u0301", decomposed);

        Assert.Same(upper, _storage.Get("Ship"));
        Assert.Same(lower, _storage.Get("ship"));
        Assert.Same(composed, _storage.Get("\u00e9"));
        Assert.Same(decomposed, _storage.Get("e\u0301"));
        Assert.Throws<KeyNotFoundException>(() => _storage.Get("SHIP"));
        Assert.False(_storage.Remove("SHIP"));
        Assert.True(_storage.Remove("Ship"));
        Assert.Same(lower, _storage.Get("ship"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Invalid_Ids_Are_Rejected_By_All_Id_Operations(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => _storage.Add(id!, new TestGameItem()));
        Assert.ThrowsAny<ArgumentException>(() => _storage.Get(id!));
        Assert.ThrowsAny<ArgumentException>(() => _storage.Remove(id!));
        Assert.Empty(_storage.GetSnapshot());
    }

    [Fact]
    public void Null_Item_Is_Rejected_Without_Reserving_The_Id()
    {
        Assert.Throws<ArgumentNullException>(() => _storage.Add("Ship", null!));
        Assert.Empty(_storage.GetSnapshot());
        var item = new TestGameItem();

        _storage.Add("Ship", item);

        Assert.Same(item, _storage.Get("Ship"));
    }

    [Fact]
    public void Storages_Have_Independent_Entries()
    {
        IGameItemStorage other = new GameItemStorage();
        var first = new TestGameItem();
        var second = new TestGameItem();
        _storage.Add("Ship", first);

        Assert.Empty(other.GetSnapshot());
        Assert.Throws<KeyNotFoundException>(() => other.Get("Ship"));
        other.Add("Ship", second);
        _storage.Remove("Ship");

        Assert.Empty(_storage.GetSnapshot());
        Assert.Same(second, other.Get("Ship"));
    }

    [Fact]
    public void Snapshot_Preserves_Membership_After_Additions_Removals_And_Id_Reuse()
    {
        var original = new TestGameItem();
        var retained = new TestGameItem();
        var replacement = new TestGameItem();
        var added = new TestGameItem();
        var emptySnapshot = _storage.GetSnapshot();
        _storage.Add("Ship", original);
        _storage.Add("Retained", retained);
        var snapshot = _storage.GetSnapshot();

        _storage.Add("Added", added);
        _storage.Remove("Ship");
        _storage.Add("Ship", replacement);

        Assert.Empty(emptySnapshot);
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(original, snapshot);
        Assert.Contains(retained, snapshot);
        Assert.DoesNotContain(added, snapshot);
        Assert.DoesNotContain(replacement, snapshot);
        var current = _storage.GetSnapshot();
        Assert.Equal(3, current.Count);
        Assert.Contains(retained, current);
        Assert.Contains(added, current);
        Assert.Contains(replacement, current);
        Assert.DoesNotContain(original, current);
    }

    [Fact]
    public void Storage_And_Snapshot_Preserve_Original_Item_References()
    {
        var item = new TestGameItem();
        _storage.Add("Ship", item);
        var snapshot = _storage.GetSnapshot();

        item.SetProperty("Value", 1);

        Assert.Same(item, _storage.Get("Ship"));
        Assert.Same(item, Assert.Single(snapshot));
        Assert.Equal(1, _storage.Get("Ship").GetProperty("Value"));
        Assert.Equal(1, Assert.Single(snapshot).GetProperty("Value"));
        _storage.Get("Ship").SetProperty("Value", 2);
        Assert.Equal(2, item.GetProperty("Value"));
        _storage.Remove("Ship");
        item.SetProperty("Value", 3);
        Assert.Equal(3, Assert.Single(snapshot).GetProperty("Value"));
    }

    [Fact]
    public void The_Same_Item_Can_Have_Multiple_Ids_And_Removing_One_Preserves_The_Other()
    {
        var item = new Mock<IGameItem>(MockBehavior.Strict);
        _storage.Add("First", item.Object);
        _storage.Add("Second", item.Object);

        Assert.Equal(2, _storage.GetSnapshot().Count);
        Assert.All(_storage.GetSnapshot(), stored => Assert.Same(item.Object, stored));
        Assert.True(_storage.Remove("First"));
        Assert.Same(item.Object, _storage.Get("Second"));
        Assert.True(_storage.Remove("Second"));
        item.VerifyNoOtherCalls();
    }

    [Fact]
    public void Snapshot_Does_Not_Expose_A_Mutable_Collection()
    {
        var item = new TestGameItem();
        _storage.Add("Ship", item);
        var snapshot = _storage.GetSnapshot();

        Assert.False(snapshot is IDictionary<string, IGameItem>);
        var collection = Assert.IsAssignableFrom<ICollection<IGameItem>>(snapshot);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Throws<NotSupportedException>(() => collection.Add(new TestGameItem()));
        Assert.Throws<NotSupportedException>(() => collection.Remove(item));
        Assert.Same(item, Assert.Single(snapshot));
        Assert.Same(item, _storage.Get("Ship"));
    }
}
