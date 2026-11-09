namespace SpaceBattle.Lib;

public class HardStopCommand : ICommand
{
    private readonly IServerThread _thread;
    private readonly IExceptionFactory _exceptions;

    public HardStopCommand(IServerThread thread, IExceptionFactory exceptions)
    {
        _thread = thread;
        _exceptions = exceptions;
    }

    public void Execute()
    {
        if (Environment.CurrentManagedThreadId != _thread.ThreadId)
        {
            throw _exceptions.InvalidOperation("HardStop can only be executed by the thread it stops.");
        }
        _thread.HardStop();
    }
}
