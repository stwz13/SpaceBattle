using System.Text.Json;

namespace SpaceBattle.Lib;

public interface IGameObjectRuleValue
{
    string Name { get; }
    Type PropertyType { get; }
    Func<IGameItem, object> Read(JsonElement value);
}
