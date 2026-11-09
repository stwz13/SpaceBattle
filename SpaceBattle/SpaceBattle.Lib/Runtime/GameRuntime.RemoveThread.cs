namespace SpaceBattle.Lib;

public sealed partial class GameRuntime
{
    internal IThreadRemoval ThreadRemoval { get; set; } = null!;
    public Task RemoveThread(string id) => ThreadRemoval.RemoveThread(id);
}
