namespace SpaceBattle.Lib;

public interface IExceptionHandler
{
    void Handle(ICommand command, Exception exception);
}
