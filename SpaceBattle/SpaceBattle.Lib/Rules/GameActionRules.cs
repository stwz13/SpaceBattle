using System.Collections.ObjectModel;
using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class GameActionRules
{
    public GameActionRules(IReadOnlyDictionary<string, string[]> actions,
        IReadOnlyDictionary<string, JsonElement>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var definitions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (action, steps) in actions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(action);
            if (steps is null || steps.Length == 0 || steps.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException($"Action '{action}' requires named command steps.", nameof(actions));
            definitions.Add(action, Array.AsReadOnly(steps.ToArray()));
        }
        Actions = new ReadOnlyDictionary<string, IReadOnlyList<string>>(definitions);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (name, value) in parameters ?? new Dictionary<string, JsonElement>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            values.Add(name, value.Clone());
        }
        Parameters = new ReadOnlyDictionary<string, JsonElement>(values);
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Actions { get; }
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; }
}
