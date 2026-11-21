using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class GameObjectRuleValue<T> : IGameObjectRuleValue where T : notnull
{
    private readonly Func<JsonElement, Func<IGameItem, T>> _read;

    public GameObjectRuleValue(string name, Func<JsonElement, Func<IGameItem, T>> read)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(read);
        Name = name;
        _read = read;
    }

    public string Name { get; }
    public Type PropertyType => typeof(T);

    public Func<IGameItem, object> Read(JsonElement value)
    {
        var evaluate = _read(value);
        return item => evaluate(item);
    }

    public static GameObjectRuleValue<T> Constant(string name, Func<JsonElement, T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new GameObjectRuleValue<T>(name, json =>
        {
            var value = read(json);
            return _ => value;
        });
    }
}
