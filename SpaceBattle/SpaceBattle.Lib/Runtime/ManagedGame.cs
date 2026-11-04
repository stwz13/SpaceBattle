namespace SpaceBattle.Lib;

/// <summary>Execution can be suspended between quanta; the underlying queue remains intact.</summary>
public sealed class ManagedGame(IGame inner, IGameScope scope) : IManagedGame
{
    public IGameScope Scope { get; } = scope;
    public bool Paused { get; set; }
    public string Id => inner.Id;
    public bool HasCommands => !Paused && inner.HasCommands;
    public IReadOnlyCollection<ICommand> Pending => inner.Pending;
    public void Post(ICommand command) => inner.Post(command);
    public void Execute()
    {
        if (!Paused) inner.Execute();
    }
}
