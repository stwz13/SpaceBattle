using System.Text.Json;

namespace SpaceBattle.Lib;

public interface ISnapshotBatchStore : ISnapshotStore
{
    void SaveAll(IReadOnlyDictionary<string, string> snapshots);
}

/// <summary>One atomic file replacement publishes the complete server snapshot, removing stale games.</summary>
public sealed class FileSnapshotStore(string path, object gate) : ISnapshotBatchStore
{
    public IReadOnlyCollection<string> GameIds { get { lock (gate) return Read().Keys.ToArray(); } }
    public string Load(string gameId) { lock (gate) return Read()[gameId]; }
    public void Save(string gameId, string json)
    {
        lock (gate)
        {
            var snapshots = Read();
            snapshots[gameId] = json;
            SaveAll(snapshots);
        }
    }
    public void SaveAll(IReadOnlyDictionary<string, string> snapshots)
    {
        lock (gate)
        {
            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(snapshots));
                File.Move(temporary, fullPath, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    private Dictionary<string, string> Read() => File.Exists(path)
        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
            ?? throw new FormatException("Invalid snapshot store.")
        : Enumerable.Empty<KeyValuePair<string, string>>().ToDictionary();
}
