namespace SpaceBattle.Lib;

public class BattleContext : IBattleContext
{
    public string Id { get; }
    public IGameItemStorage Storage { get; }

    public BattleContext(string id, IGameItemStorage storage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(storage);
        Id = id;
        Storage = storage;
    }
}
