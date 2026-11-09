using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyExceptionFactory : ICommand
{
    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", "Factories.Exception",
        (object[] args) => new ExceptionFactory()).Execute();
}
