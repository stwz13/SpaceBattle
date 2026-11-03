namespace SpaceBattle.Lib;

public sealed partial class GameRuntime
{
    internal Func<string, Func<IServerThread, Task>, Task> EvacuateThread { get; set; } = null!;
    public async Task RemoveThread(string id)
    {
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try { await EvacuateThread(id, _ => RebalanceCore()).ConfigureAwait(false); }
        finally { lifecycle.Release(); }
    }
}
