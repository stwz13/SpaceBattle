using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyExceptionFactory : ICommand
{
    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", ExceptionDependencies.Factory,
        (object[] args) => new ExceptionFactory()).Execute();
}
