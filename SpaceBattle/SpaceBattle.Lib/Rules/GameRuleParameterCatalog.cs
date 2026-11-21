using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class GameRuleParameterCatalog(IEnumerable<IGameRuleParameter> handlers, GameObjectRuleSchema? objects = null)
{
    private readonly IReadOnlyDictionary<string, IGameRuleParameter> _handlers =
        handlers.ToDictionary(handler => handler.Name, StringComparer.Ordinal);

    private static readonly Lazy<GameRuleParameterCatalog> BuiltIn = new(() =>
        new([new ShipSpeedRule(), new ProjectileSettingsRule()], GameObjectRuleSchema.Load(GameObjectRuleSchema.DefaultPath)));

    public static GameRuleParameterCatalog Standard => BuiltIn.Value;

    public void Validate(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            if (_handlers.TryGetValue(name, out var handler)) handler.Validate(value);
            else if (objects is not null && GameObjectRuleSchema.Supports(name)) objects.Validate(name, value);
        }
    }

    public void Install(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        Validate(parameters);
        foreach (var (name, value) in parameters)
        {
            if (_handlers.TryGetValue(name, out var handler)) handler.Install(value);
            else if (objects is not null && GameObjectRuleSchema.Supports(name)) objects.Install(name, value);
        }
    }
}
