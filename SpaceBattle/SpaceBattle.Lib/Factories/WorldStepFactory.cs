namespace SpaceBattle.Lib;

public sealed class WorldStepFactory : IWorldStepFactory
{
    private readonly Func<IGameItem> _createFrame;
    private readonly Func<IGameItem, ICommand> _compose;

    public WorldStepFactory(Func<IGameItem> createFrame, Func<IGameItem, ICommand> compose)
    {
        ArgumentNullException.ThrowIfNull(createFrame);
        ArgumentNullException.ThrowIfNull(compose);
        _createFrame = createFrame;
        _compose = compose;
    }

    public ICommand Create() => _compose(_createFrame());
}
