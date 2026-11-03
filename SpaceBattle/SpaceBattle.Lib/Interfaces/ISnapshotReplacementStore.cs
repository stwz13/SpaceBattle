namespace SpaceBattle.Lib;

public interface ISnapshotReplacementStore : ISnapshotStore
{
    void ReplaceAll(IReadOnlyDictionary<string, string> snapshots);
}
