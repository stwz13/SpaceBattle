using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyCollisionContextCommand : ICommand
{
    private readonly string _dependencyKey;
    private readonly string _treeKey;
    private readonly string _firstRole;
    private readonly string _secondRole;
    private readonly string _consequenceAction;

    public RegisterIoCDependencyCollisionContextCommand(string dependencyKey, string treeKey,
        string firstRole, string secondRole, string consequenceAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(treeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(consequenceAction);
        if (firstRole == secondRole)
        {
            throw new ArgumentException("A collision needs two different roles.", nameof(secondRole));
        }
        _dependencyKey = dependencyKey;
        _treeKey = treeKey;
        _firstRole = firstRole;
        _secondRole = secondRole;
        _consequenceAction = consequenceAction;
    }

    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", _dependencyKey, (object[] args) =>
        {
            if (args.Length != 1 || args[0] is not ICommandContext context)
            {
                throw new ArgumentException("A context command requires an ICommandContext.");
            }
            var first = context.GetItem(_firstRole);
            var second = context.GetItem(_secondRole);
            return new CollisionCommand(
                Ioc.Resolve<CollisionTree>(_treeKey),
                () => CollisionOffset.Between(first, second),
                Ioc.Resolve<ICommand>(CommandContextDependencies.Action, _consequenceAction, context));
        }).Execute();
    }
}
