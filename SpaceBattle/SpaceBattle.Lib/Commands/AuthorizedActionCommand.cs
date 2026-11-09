namespace SpaceBattle.Lib;

public sealed class AuthorizedActionCommand : ICommand
{
    private readonly Func<bool> _isAllowed;
    private readonly Func<ICommand> _createAction;
    private readonly Action _onDenied;

    public AuthorizedActionCommand(Func<bool> isAllowed, Func<ICommand> createAction, Action onDenied)
    {
        ArgumentNullException.ThrowIfNull(isAllowed);
        ArgumentNullException.ThrowIfNull(createAction);
        ArgumentNullException.ThrowIfNull(onDenied);
        _isAllowed = isAllowed;
        _createAction = createAction;
        _onDenied = onDenied;
    }

    public void Execute()
    {
        if (!_isAllowed())
        {
            _onDenied();
            return;
        }
        _createAction().Execute();
    }
}
