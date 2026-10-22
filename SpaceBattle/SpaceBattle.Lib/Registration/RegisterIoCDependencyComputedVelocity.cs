using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyComputedVelocity : ICommand
{
    private readonly string _dependencyKey;
    private readonly string _speedProperty;
    private readonly string _directionProperty;

    public RegisterIoCDependencyComputedVelocity(
        string dependencyKey = ComputedVelocityDependencies.Velocity,
        string speedProperty = "Speed",
        string directionProperty = "Direction")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(speedProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(directionProperty);
        _dependencyKey = dependencyKey;
        _speedProperty = speedProperty;
        _directionProperty = directionProperty;
    }

    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", _dependencyKey,
        (object[] args) =>
        {
            var item = (IGameItem)args[0];
            var speed = (int)item.GetProperty(_speedProperty);
            var direction = (Angle)item.GetProperty(_directionProperty);
            ArgumentOutOfRangeException.ThrowIfNegative(speed);
            return new Vector(
                checked((int)Math.Round(speed * Math.Cos(direction.Radians), MidpointRounding.AwayFromZero)),
                checked((int)Math.Round(speed * Math.Sin(direction.Radians), MidpointRounding.AwayFromZero)));
        }).Execute();
}
