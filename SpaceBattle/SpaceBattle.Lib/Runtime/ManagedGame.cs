namespace SpaceBattle.Lib;

public sealed class ManagedGame(IGame inner, IGameScope scope, IGameExecutionState state) : IManagedGame
{
    public IGameScope Scope { get; } = scope;
    public bool Paused { get => state.Paused; set => state.Paused = value; }
    public string Id => inner.Id;
    public bool HasCommands => !Paused && inner.HasCommands;
    public IReadOnlyCollection<ICommand> Pending => inner.Pending;
    public void Post(ICommand command) => inner.Post(command);
    public void Execute() { if (!Paused) inner.Execute(); }
}
