using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyDamageContextCommand : ICommand
{
    private readonly string _dependencyKey;
    private readonly string _targetRole;
    private readonly string _sourceRole;

    public RegisterIoCDependencyDamageContextCommand(string dependencyKey, string targetRole, string sourceRole)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRole);
        if (targetRole == sourceRole)
        {
            throw new ArgumentException("Damage needs two different roles.", nameof(sourceRole));
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
            return new ApplyDamageCommand(
                Adapt<IDamageTarget>(context.GetItem(_targetRole)),
                Adapt<IDamageSource>(context.GetItem(_sourceRole)));
        }).Execute();
    }

    private static T Adapt<T>(IGameItem item) =>
        Ioc.Resolve<T>(GeneratedNames.AdapterDependency(typeof(T)), item);
}
