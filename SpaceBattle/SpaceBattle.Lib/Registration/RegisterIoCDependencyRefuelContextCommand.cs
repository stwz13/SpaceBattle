using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyRefuelContextCommand : ICommand
{
    private readonly string _dependencyKey;
    private readonly string _targetRole;
    private readonly string _sourceRole;

    public RegisterIoCDependencyRefuelContextCommand(string dependencyKey, string targetRole, string sourceRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRole);
        if (targetRole == sourceRole)
        {
            throw new ArgumentException("Refuelling needs two different roles.", nameof(sourceRole));
        }
        _dependencyKey = dependencyKey;
        _targetRole = targetRole;
        _sourceRole = sourceRole;
    }

    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", _dependencyKey, (object[] args) =>
        {
            if (args.Length != 1 || args[0] is not ICommandContext context)
            {
                throw new ArgumentException("A context command requires an ICommandContext.");
            }
            return new RefuelCommand(
                Adapt<IRefuelTarget>(context.GetItem(_targetRole)),
                Adapt<IRefuelSource>(context.GetItem(_sourceRole)));
        }).Execute();
    }

    private static T Adapt<T>(IGameItem item) =>
        Ioc.Resolve<T>(GeneratedNames.AdapterDependency(typeof(T)), item);
}
