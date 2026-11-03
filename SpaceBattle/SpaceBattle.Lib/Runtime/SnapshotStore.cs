using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

public interface ISnapshotStore
{
    IReadOnlyCollection<string> GameIds { get; }
    void Save(string gameId, string json);
    string Load(string gameId);
}

public sealed class MemorySnapshotStore(ConcurrentDictionary<string, string> snapshots) : ISnapshotReplacementStore
{
    public IReadOnlyCollection<string> GameIds { get { lock (snapshots) return snapshots.Keys.ToArray(); } }
    public void Save(string gameId, string json) { lock (snapshots) snapshots[gameId] = json; }
    public string Load(string gameId) { lock (snapshots) return snapshots[gameId]; }
    public void ReplaceAll(IReadOnlyDictionary<string, string> values)
    {
        lock (snapshots)
        {
            snapshots.Clear();
            values.ToList().ForEach(pair => snapshots[pair.Key] = pair.Value);
        }
    }
}
