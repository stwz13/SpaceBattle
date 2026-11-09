using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class FileSnapshotStoreTests
{
    [Fact]
    public void Whole_Server_Save_Replaces_The_Generation_And_Preserves_Arbitrary_Game_Ids()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new FileSnapshotStore(path, new object());
            store.Save("../../old", "old");
            store.SaveAll(new Dictionary<string, string> { ["../current"] = "{\"items\":[]}" });
            var reopened = new FileSnapshotStore(path, new object());
            Assert.Equal(new[] { "../current" }, reopened.GameIds);
            Assert.Equal("{\"items\":[]}", reopened.Load("../current"));
            Assert.Throws<KeyNotFoundException>(() => reopened.Load("../../old"));
            reopened.SaveAll(new Dictionary<string, string>());
            Assert.Empty(reopened.GameIds);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
