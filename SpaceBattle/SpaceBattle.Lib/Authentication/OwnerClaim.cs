using System.Text.Json;
namespace SpaceBattle.Lib;

public static class OwnerClaim
{
    public static string? From(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("properties", out var properties)) return null;
        if (properties.ValueKind != JsonValueKind.Object || !properties.TryGetProperty("Owner", out var owner)) return null;
        if (owner.ValueKind != JsonValueKind.Object || !owner.TryGetProperty("type", out var type)) return null;
        if (type.ValueKind != JsonValueKind.String || type.GetString() != "string") return null;
        return owner.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
