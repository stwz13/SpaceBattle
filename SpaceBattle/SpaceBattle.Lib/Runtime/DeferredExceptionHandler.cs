namespace SpaceBattle.Lib;

public sealed class DeferredExceptionHandler : IExceptionHandler
{
    private IExceptionHandler _handler = null!;
    public void Bind(IExceptionHandler handler) => _handler = handler;
    public void Handle(ICommand command, Exception exception) => _handler.Handle(command, exception);
}
