namespace SpaceBattle.Lib;

public sealed class LoggingExceptionHandler(IErrorRecovery recovery) : IExceptionHandler
{
    public void Handle(ICommand command, Exception exception)
    {
        var firstAttempt = true;
        recovery.Execute(() =>
        {
            if (firstAttempt) { firstAttempt = false; throw exception; }
            command.Execute();
        });
    }
}
