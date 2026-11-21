using App;
using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class GameObjectRuleSchema
{
    private readonly IReadOnlyDictionary<string, Field> _fields;

    private GameObjectRuleSchema(IReadOnlyDictionary<string, Field> fields) => _fields = fields;

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Config", "ObjectRules.json");

    public static GameObjectRuleSchema Load(string path, IEnumerable<IGameObjectRuleValue>? additionalValues = null,
        IEnumerable<Type>? additionalViews = null)
    {
        using var source = File.OpenRead(path);
        var fields = JsonSerializer.Deserialize<Dictionary<string, Definition>>(source, JsonSerializerOptions.Web)
            ?? throw new FormatException("Expected an object rule schema.");
        var converters = BuiltInValues().Concat(additionalValues ?? []).ToDictionary(value => value.Name, StringComparer.Ordinal);
        var views = typeof(GameObjectRuleSchema).Assembly.GetExportedTypes().Concat(additionalViews ?? [])
            .Where(InterfaceInspector.IsAdaptable).Distinct().ToDictionary(view => view.FullName!, StringComparer.Ordinal);
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new Dictionary<string, Field>(StringComparer.Ordinal);
        foreach (var (name, field) in fields)
        {
            if (string.IsNullOrWhiteSpace(name) || field is not { View: { } viewName, Property: { } propertyName, Type: { } type } ||
                !converters.TryGetValue(type, out var converter) || !views.TryGetValue(viewName, out var view))
                throw new FormatException($"Invalid object rule schema field '{name}'.");
            var property = InterfaceInspector.Properties(view).SingleOrDefault(p => p.Name == propertyName);
            if (property is null || !property.CanRead || property.PropertyType != converter.PropertyType)
                throw new FormatException($"Incompatible property for object rule field '{name}'.");
            var dependency = GeneratedNames.PropertyDependency(view, property);
            if (!dependencies.Add(dependency))
                throw new FormatException($"Duplicate property for object rule field '{name}'.");
            resolved.Add(name, new Field(dependency, converter, field.Min));
        }
        return new GameObjectRuleSchema(resolved);
    }

    internal static bool Supports(string name) => name.StartsWith(GameRuleParameterNames.ObjectPrefix, StringComparison.Ordinal);

    internal void Validate(string name, JsonElement value) => _ = Read(name, value);

    internal void Install(string name, JsonElement value)
    {
        var properties = Read(name, value);
        var bindings = Ioc.Resolve<GameObjectRuleBindings>(GameObjectRuleBindings.Dependency);
        var kind = name[GameRuleParameterNames.ObjectPrefix.Length..];
        foreach (var (dependency, read) in properties)
            bindings.Add(dependency, kind, read, legacy: false);
    }

    private Dictionary<string, Func<IGameItem, object>> Read(string name, JsonElement value)
    {
        if (string.IsNullOrWhiteSpace(name[GameRuleParameterNames.ObjectPrefix.Length..]) || value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Expected object settings for '{name}'.");
        var properties = new Dictionary<string, Func<IGameItem, object>>(StringComparer.Ordinal);
        foreach (var entry in value.EnumerateObject())
        {
            if (!_fields.TryGetValue(entry.Name, out var field))
                throw new ArgumentException($"Unknown object rule field '{name}.{entry.Name}'.");
            try
            {
                if (field.Min is int min && entry.Value.GetInt32() < min)
                    throw new ArgumentException($"'{name}.{entry.Name}' must be at least {min}.");
                if (!properties.TryAdd(field.Dependency, field.Converter.Read(entry.Value)))
                    throw new ArgumentException($"Duplicate object rule field '{name}.{entry.Name}'.");
            }
            catch (Exception error) when (error is InvalidOperationException or FormatException or JsonException)
            {
                throw new ArgumentException($"Invalid value for '{name}.{entry.Name}'.", nameof(value), error);
            }
        }
        return properties;
    }

    private static IGameObjectRuleValue[] BuiltInValues() =>
    [
        GameObjectRuleValue<int>.Constant("integer", value => value.GetInt32()),
        GameObjectRuleValue<Angle>.Constant("angleSteps", value => new Angle(value.GetInt32())),
        new GameObjectRuleValue<Vector>("speed", value =>
        {
            var speed = value.GetInt32();
            return item => VelocityCalculation.FromSpeedAndDirection(speed, (Angle)item.GetProperty("Direction"));
        })
    ];

    private sealed record Definition(string View, string Property, string Type, int? Min);
    private sealed record Field(string Dependency, IGameObjectRuleValue Converter, int? Min);
}
