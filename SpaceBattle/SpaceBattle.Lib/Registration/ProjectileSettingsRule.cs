using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class ProjectileSettingsRule : GameClassParameter<ProjectileSettingsRule.Settings>
{
    public sealed record Settings(int Speed, int Damage);

    public ProjectileSettingsRule() : base("Shooting.Projectile", Read, Bind) { }

    private static Settings Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            value.EnumerateObject().Any(p => p.Name is not ("speed" or "damage")))
            throw new ArgumentException("Shooting.Projectile expects an object containing speed and damage.");
        return new Settings(Number(value, "speed"), Number(value, "damage"));
    }

    private static int Number(JsonElement value, string name)
    {
        if (value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number &&
            field.TryGetInt32(out var number) && number >= 0) return number;
        throw new ArgumentException($"Shooting.Projectile.{name} must be a nonnegative integer.");
    }

    private static void Bind(Settings settings)
    {
        SetProperty(nameof(IShooting.ProjectileSpeed), settings.Speed);
        SetProperty(nameof(IShooting.ProjectileDamage), settings.Damage);
    }

    private static void SetProperty(string name, int value)
    {
        var property = typeof(IShooting).GetProperty(name)!;
        IoCRegistrar.Register(GeneratedNames.PropertyDependency(typeof(IShooting), property), _ => value);
    }
}
