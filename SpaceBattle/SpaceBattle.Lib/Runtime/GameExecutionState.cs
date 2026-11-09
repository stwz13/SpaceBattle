namespace SpaceBattle.Lib;

public sealed class GameExecutionState : IGameExecutionState
{
    private int _paused;
    public bool Paused { get => Volatile.Read(ref _paused) != 0; set => Volatile.Write(ref _paused, value ? 1 : 0); }
}
