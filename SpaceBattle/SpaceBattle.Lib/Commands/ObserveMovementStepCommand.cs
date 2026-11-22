namespace SpaceBattle.Lib;

internal sealed class ObserveMovementStepCommand : ICommand
{
    private readonly ICommand _step;
    private readonly Func<Vector> _position;
    private readonly Action<Vector, Vector> _observe;

    public ObserveMovementStepCommand(ICommand step, Func<Vector> position, Action<Vector, Vector> observe)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(observe);
        _step = step;
        _position = position;
        _observe = observe;
    }

    public void Execute()
    {
        var start = new Vector(_position().Coordinates);
        _step.Execute();
        var end = new Vector(_position().Coordinates);
        _observe(start, end);
    }
}
