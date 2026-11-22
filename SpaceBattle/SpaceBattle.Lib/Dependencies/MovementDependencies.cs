namespace SpaceBattle.Lib;

public static class MovementDependencies
{
    public const string CreateOperation = "Movement.CreateOperation";

    public static Dependency ComputedVelocity { get; } = new Dependency(
        typeof(IMoving),
        nameof(IMoving.Velocity),
        ComputedVelocityDependencies.Velocity);
}
