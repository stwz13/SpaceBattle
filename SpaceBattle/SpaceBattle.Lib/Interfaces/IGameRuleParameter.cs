using System.Text.Json;

namespace SpaceBattle.Lib;

public interface IGameRuleParameter
{
    string Name { get; }
    void Validate(JsonElement value);
    void Install(JsonElement value);
}
