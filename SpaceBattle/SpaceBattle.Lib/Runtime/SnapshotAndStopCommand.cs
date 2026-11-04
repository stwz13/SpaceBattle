namespace SpaceBattle.Lib;

public sealed class SnapshotAndStopCommand(IServerSnapshots runtime, ISnapshotStore store) : ICommand
{
    public Task<IReadOnlyDictionary<string, string>>? Completion { get; private set; }
    public void Execute()
    {
        if (Completion is not null) throw new InvalidOperationException("The snapshot command has already been executed.");
        Completion = runtime.SnapshotAndStop(store);
    }
}
