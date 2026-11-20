using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class GameRuleParameterCatalog(IEnumerable<IGameRuleParameter> handlers)
{
    private readonly IReadOnlyDictionary<string, IGameRuleParameter> _handlers =
        handlers.ToDictionary(handler => handler.Name, StringComparer.Ordinal);

    public static GameRuleParameterCatalog Standard { get; } = new([new ShipSpeedRule(), new ProjectileSettingsRule()]);

    public void Validate(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        foreach (var (name, value) in parameters)
            if (_handlers.TryGetValue(name, out var handler)) handler.Validate(value);
    }

    public void Install(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        Validate(parameters);
        foreach (var (name, value) in parameters)
            if (_handlers.TryGetValue(name, out var handler)) handler.Install(value);
    }
}
