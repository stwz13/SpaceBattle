using App;
using System.Text.Json;

namespace SpaceBattle.Lib;

public class GameObjectRuleParameter<TSettings> : GameClassParameter<TSettings>
{
    public GameObjectRuleParameter(string kind, Func<JsonElement, TSettings> read,
        params GameObjectRuleProperty<TSettings>[] properties)
        : this(GameRuleParameterNames.ForObject(kind), kind, read, properties, legacy: false) { }

    internal GameObjectRuleParameter(string name, string kind, Func<JsonElement, TSettings> read,
        GameObjectRuleProperty<TSettings>[] properties, bool legacy)
        : base(name, read, Bind(kind, properties, legacy)) { }

    private static Action<TSettings> Bind(string kind, GameObjectRuleProperty<TSettings>[] properties, bool legacy)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var bindings = properties.ToArray();
        return settings =>
        {
            var scope = Ioc.Resolve<GameObjectRuleBindings>(GameObjectRuleBindings.Dependency);
            foreach (var binding in bindings)
                binding.Bind(scope, kind, settings, legacy);
        };
    }
}
