using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class JsonWorldPhaseConfigurationLoader : IWorldPhaseConfigurationLoader
{
    public IReadOnlyList<WorldPhaseDescription> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        RequireFields(root, "phases");
        if (!root.TryGetProperty("phases", out var phases) || phases.ValueKind != JsonValueKind.Array)
            throw new FormatException("World phase configuration requires a phases array.");

        var result = new List<WorldPhaseDescription>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var phase in phases.EnumerateArray())
        {
            RequireFields(phase, "name", "execution", "action", "selector");
            var name = RequiredString(phase, "name");
            var action = RequiredString(phase, "action");
            var execution = RequiredString(phase, "execution") switch
            {
                "world" => WorldPhaseExecution.World,
                "items" => WorldPhaseExecution.Items,
                var value => throw new FormatException($"Unknown execution mode '{value}' in phase '{name}'.")
            };
            var selector = execution == WorldPhaseExecution.Items
                ? RequiredString(phase, "selector") : null;
            if (execution == WorldPhaseExecution.World && phase.TryGetProperty("selector", out _))
                throw new FormatException($"World phase '{name}' cannot specify a selector.");
            if (!names.Add(name))
                throw new FormatException($"Duplicate world phase '{name}'.");
            result.Add(new WorldPhaseDescription(name, execution, action, selector));
        }
        return result.AsReadOnly();
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new FormatException($"World phase requires a nonempty string '{name}'.");
        return value.GetString()!;
    }

    private static void RequireFields(JsonElement element, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException("World phase configuration entries must be objects.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new FormatException($"Unknown world phase configuration field '{property.Name}'.");
            if (!seen.Add(property.Name))
                throw new FormatException($"Duplicate world phase configuration field '{property.Name}'.");
        }
    }
}
