using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace SpaceBattle.Lib;

internal static class PlayerProgramProtocol
{
    public static ProgramOrder? ReadOrder(string json, IPlayerProgramLimits limits)
    {
        if (Encoding.UTF8.GetByteCount(json) > limits.OutputBytes)
            throw new PlayerProgramException("The program exceeded the output limit.");

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = limits.JsonDepth,
                AllowDuplicateProperties = false
            });
            var args = document.RootElement;
            if (args.ValueKind == JsonValueKind.Null) return null;
            if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("id", out var id) || !IsText(id) ||
                !args.TryGetProperty("action", out var action) || !IsText(action))
                throw new PlayerProgramException("A program must return null or an order with id and action.");

            return new ProgramOrder(id.GetString()!, action.GetString()!,
                args.TryGetProperty("roles", out var roles) ? ReadRoles(roles) : null);
        }
        catch (JsonException)
        {
            throw new PlayerProgramException("A program must return valid JSON without duplicate fields.");
        }
    }

    private static IReadOnlyDictionary<string, string> ReadRoles(JsonElement roles)
    {
        if (roles.ValueKind != JsonValueKind.Object)
            throw new PlayerProgramException("Order roles must map role names to item identifiers.");

        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var role in roles.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(role.Name) || !IsText(role.Value))
                throw new PlayerProgramException("Order roles must map role names to item identifiers.");
            mapping.Add(role.Name, role.Value.GetString()!);
        }
        return new ReadOnlyDictionary<string, string>(mapping);
    }

    private static bool IsText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());
}
