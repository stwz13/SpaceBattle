using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameItemFactory : ICommand
{
    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", GameItemFactoryDependencies.Factory,
        (object[] args) => new GameItemFactory()).Execute();
}
