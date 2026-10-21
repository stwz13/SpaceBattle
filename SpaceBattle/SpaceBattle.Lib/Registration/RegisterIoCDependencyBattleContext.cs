using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyBattleContext : ICommand
{
    private readonly IBattleContext _context;

    public RegisterIoCDependencyBattleContext(IBattleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public void Execute()
    {
        new RegisterIoCDependencyGameItemStorage(_context.Storage).Execute();
        Ioc.Resolve<App.ICommand>("IoC.Register", BattleContextDependencies.Context,
            (object[] args) => _context).Execute();
    }
}
