namespace SpaceBattle.Lib;

internal sealed class RepeatOperationCommand : ICommand
{
    private readonly ICommand _step;
    private readonly ICommand _send;
    private readonly Func<bool> _isActive;
    private readonly Action _stop;

    public RepeatOperationCommand(ICommand step, ICommand send, Func<bool> isActive, Action stop)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(isActive);
        ArgumentNullException.ThrowIfNull(stop);
        _step = step;
        _send = send;
        _isActive = isActive;
        _stop = stop;
    }

    public void Execute()
    {
        try
        {
            _step.Execute();
            if (_isActive())
                _send.Execute();
        }
        catch
        {
            _stop();
            throw;
        }
    }
}
