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

    public void Execute() =>
        new RegisterIoCDependencyWorldCollisionRules([CreateRule]).Execute();

    private ICommand CreateRule(WorldCollisionFrame frame)
    {
        var tree = Ioc.Resolve<CollisionTree>(_treeKey);
        return new CheckWorldCollisionsCommand(frame, _isTarget, _isSource,
            (target, source, offset) => new CollisionCommand(tree, offset,
                new CallbackCommand(() => ExecuteConsequence(target, source))));
    }

    private void ExecuteConsequence(IGameItem target, IGameItem source)
    {
        var context = new CommandContext(new Dictionary<string, IGameItem> { ["Target"] = target, ["Source"] = source });
        Ioc.Resolve<ICommand>(CommandContextDependencies.Action, _consequenceAction, context).Execute();
    }
}
