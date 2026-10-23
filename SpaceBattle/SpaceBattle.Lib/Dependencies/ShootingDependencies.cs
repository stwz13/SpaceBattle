namespace SpaceBattle.Lib;

public static class ShootingDependencies
{
    public const string Factory = DependencyNamespaces.Factories + ".Projectile";

    public static IReadOnlyList<Dependency> All { get; } = Array.AsReadOnly(new[]
    {
        new Dependency(typeof(IShooting), nameof(IShooting.ProjectileFactory), Factory),
        new Dependency(typeof(IShooting), nameof(IShooting.Storage), GameItemStorageDependencies.Storage),
        new Dependency(typeof(IShooting), nameof(IShooting.ExceptionFactory), "Factories.Exception")
    });
}
