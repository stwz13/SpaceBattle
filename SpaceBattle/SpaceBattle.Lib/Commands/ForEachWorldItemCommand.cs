namespace SpaceBattle.Lib;

public sealed class ForEachWorldItemCommand : ICommand
{
    private readonly IReadOnlyCollection<IGameItem> _items;
    private readonly Func<IGameItem, ICommand> _createAction;
    private readonly Func<IGameItem, bool> _includes;

    public ForEachWorldItemCommand(IReadOnlyCollection<IGameItem> items,
        Func<IGameItem, ICommand> createAction, Func<IGameItem, bool> includes)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(createAction);
        ArgumentNullException.ThrowIfNull(includes);
        _items = items;
        _createAction = createAction;
        _includes = includes;
    }

    public void Execute()
    {
        foreach (var item in _items.Where(_includes))
            _createAction(item).Execute();
    }
}
