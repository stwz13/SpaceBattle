using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGame : ICommand
{
    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", GameDependencies.Create, (object[] args) =>
    {
        if (args.Length != 1 || args[0] is not string id)
        {
            throw new ArgumentException("A game requires its identifier.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var context = Ioc.Resolve<IBattleContextFactory>(BattleContextDependencies.Factory).Create(id);
        var scope = new GameScope(Ioc.Resolve<object>("IoC.Scope.Create"));
        using (scope.Enter())
        {
            new RegisterIoCDependencyBattleContext(context).Execute();
        }
        return new Game(id, new GameQueue(), scope, Ioc.Resolve<IQuantum>(GameDependencies.Quantum));
    }).Execute();
}
