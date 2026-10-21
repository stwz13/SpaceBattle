using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyBattleContextFactory : ICommand
{
    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", BattleContextDependencies.Factory,
        (object[] args) => new BattleContextFactory()).Execute();
}
