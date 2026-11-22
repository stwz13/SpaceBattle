namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyWorldCollisionRules : ICommand
{
    private readonly Func<WorldCollisionFrame, ICommand>[] _rules;

    public RegisterIoCDependencyWorldCollisionRules(IEnumerable<Func<WorldCollisionFrame, ICommand>> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
    }

    public void Execute()
    {
        var segments = new WorldMovementSegments();
        IoCRegistrar.Register(WorldCollisionDependencies.MovementSegments, args => args.Length == 0
            ? segments
            : throw new ArgumentException("World movement segments take no arguments."));
        IoCRegistrar.Register(WorldCollisionDependencies.Check, args => args is [IReadOnlyCollection<IGameItem> items]
            ? new CallbackCommand(() =>
            {
                var frame = new WorldCollisionFrame(items, segments.Take());
                foreach (var rule in _rules)
                    rule(frame).Execute();
            })
            : throw new ArgumentException("A world collision check requires a snapshot of game items."));
    }
}
