namespace SpaceBattle.Lib;

public sealed class LoggingExceptionHandler(ILog log) : IExceptionHandler
{
    public void Handle(ICommand command, Exception exception) => log.Write("server", exception, ErrorAction.Skip, 0);
}
