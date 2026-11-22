namespace SpaceBattle.Lib;

internal sealed class WorldTickCommand : ICommand
{
    private readonly IGameItemStorage _storage;
    private readonly Func<IReadOnlyCollection<IGameItem>, ICommand> _compose;

    public WorldTickCommand(IGameItemStorage storage, Func<IReadOnlyCollection<IGameItem>, ICommand> compose)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(compose);
        _storage = storage;
        _compose = compose;
    }

    public void Execute() => _compose(_storage.GetSnapshot()).Execute();
}
