namespace SpaceBattle.Lib;

public interface IExceptionFactory
{
    Exception InvalidOperation(string message);
}
