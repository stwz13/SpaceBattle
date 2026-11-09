namespace SpaceBattle.Lib;

public static class MovementDependencies
{
    public static Dependency ComputedVelocity { get; } = new Dependency(
        typeof(IMoving),
        nameof(IMoving.Velocity),
        ComputedVelocityDependencies.Velocity);
}
