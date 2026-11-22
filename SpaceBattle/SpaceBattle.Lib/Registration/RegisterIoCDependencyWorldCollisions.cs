using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyWorldCollisions : ICommand
{
    private readonly Func<IGameItem, bool> _isTarget;
    private readonly Func<IGameItem, bool> _isSource;
    private readonly string _treeKey;
    private readonly string _consequenceAction;

    public RegisterIoCDependencyWorldCollisions(
        Func<IGameItem, bool> isTarget, Func<IGameItem, bool> isSource, string treeKey, string consequenceAction)
    {
        ArgumentNullException.ThrowIfNull(isTarget);
        ArgumentNullException.ThrowIfNull(isSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(treeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(consequenceAction);

        _isTarget = isTarget;
        _isSource = isSource;
        _treeKey = treeKey;
        _consequenceAction = consequenceAction;
    }

    public void Execute()
    {
        var segments = new WorldMovementSegments();
        IoCRegistrar.Register(WorldCollisionDependencies.MovementSegments, args => args.Length == 0
            ? segments
            : throw new ArgumentException("World movement segments take no arguments."));
        IoCRegistrar.Register(WorldCollisionDependencies.Check, args =>
        {
            if (args is not [IReadOnlyCollection<IGameItem> items])
            {
                throw new ArgumentException("A world collision check requires a snapshot of game items.");
            }

            var tree = Ioc.Resolve<CollisionTree>(_treeKey);
            return new CheckWorldCollisionsCommand(items, segments.Take, _isTarget, _isSource,
                (target, source, offset) => new CollisionCommand(tree, offset,
                    new CallbackCommand(() => ExecuteConsequence(target, source))));
        });
    }

    private void ExecuteConsequence(IGameItem target, IGameItem source)
    {
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = target, ["Source"] = source });
        Ioc.Resolve<ICommand>(CommandContextDependencies.Action, _consequenceAction, context).Execute();
    }
}
