namespace SpaceBattle.Lib;

public class ExceptionFactory : IExceptionFactory
{
    public Exception InvalidOperation(string message) => new InvalidOperationException(message);
}
