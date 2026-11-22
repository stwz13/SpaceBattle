using System.Text.Json;

namespace SpaceBattle.Lib;

public static class GameRulesJson
{
    public static GameActionRules Read(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a rules object.");
        var hasActions = body.TryGetProperty("actions", out var actions);
        var hasParameters = body.TryGetProperty("parameters", out var parameters);
        if (actions.ValueKind != JsonValueKind.Object && parameters.ValueKind != JsonValueKind.Object)
            return new GameActionRules(body.Deserialize<Dictionary<string, string[]>>()!);
        if (body.EnumerateObject().Any(p => p.Name is not ("actions" or "parameters")))
            throw new JsonException("Unknown field in game rules.");
        if ((hasActions && actions.ValueKind != JsonValueKind.Object) ||
            (hasParameters && parameters.ValueKind != JsonValueKind.Object))
            throw new JsonException("Expected objects for actions and parameters.");
        return new GameActionRules(hasActions ? actions.Deserialize<Dictionary<string, string[]>>()! : new Dictionary<string, string[]>(),
            hasParameters ? parameters.Deserialize<Dictionary<string, JsonElement>>() : null);
    }
}
