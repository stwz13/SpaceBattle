using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyComputedVelocity : ICommand
{
    private readonly string _dependencyKey;
    private readonly string _speedProperty;
    private readonly string _directionProperty;
    private readonly Func<int, Angle, Vector> _calculate;

    public RegisterIoCDependencyComputedVelocity(
        string dependencyKey = ComputedVelocityDependencies.Velocity,
        string speedProperty = "Speed",
        string directionProperty = "Direction")
        : this(VelocityCalculation.FromSpeedAndDirection, dependencyKey, speedProperty, directionProperty)
    {
    }

    public RegisterIoCDependencyComputedVelocity(
        Func<int, Angle, Vector> calculate,
        string dependencyKey = ComputedVelocityDependencies.Velocity,
        string speedProperty = "Speed",
        string directionProperty = "Direction")
    {
        ArgumentNullException.ThrowIfNull(calculate);
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(speedProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(directionProperty);
        _calculate = calculate;
        _dependencyKey = dependencyKey;
        _speedProperty = speedProperty;
        _directionProperty = directionProperty;
    }

    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", _dependencyKey,
        (object[] args) =>
        {
            var item = (IGameItemPropertyReader)args[0];
            return _calculate((int)item.GetProperty(_speedProperty), (Angle)item.GetProperty(_directionProperty));
        }).Execute();
}
