using System.Reflection;

namespace SpaceBattle.Lib;

public static class GeneratedNames
{
    public const string Namespace = "SpaceBattle.Generated";
    public const string StrategiesClass = "Strategies";
    public const string StrategiesFullName = Namespace + "." + StrategiesClass;

    public static string Mangle(Type type) => type.FullName!.Replace('.', '_').Replace('+', '_');

    public static string Identifier(string prefix, Type type, string suffix = "") =>
        (string.IsNullOrEmpty(prefix) ? "" : prefix + "_") + Mangle(type) + suffix;

    public static string DependencyKey(string category, Type type, string suffix = "") =>
        DependencyKey(category, Mangle(type) + suffix);

    public static string DependencyKey(string category, params string[] components) =>
        string.Join(".", components.Prepend(category));

    // Preserve existing keys for ordinary namespace-qualified types. The reserved prefix
    // gives nested types and names with literal underscores an unambiguous token.
    private static string GeneratedTypeToken(Type type)
    {
        var name = type.FullName!;
        return name.Contains('_') || name.Contains('+')
            ? "__" + Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(name))
            : Mangle(type);
    }

    public static string AdapterClass(Type iface) => GeneratedTypeToken(iface) + "Adapter";

    public static string AdapterDependency(Type iface) => DependencyKey(DependencyNamespaces.Adapters, AdapterClass(iface));

    public static string CommandDependency(Type command) => DependencyKey(DependencyNamespaces.Commands, GeneratedTypeToken(command));

    public static string AdapterMethod(Type iface) => "Adapter_" + AdapterClass(iface);

    public static string CommandMethod(Type command) => "Command_" + GeneratedTypeToken(command);

    public static string PropertyDependency(Type view, PropertyInfo property) =>
        DependencyKey(DependencyNamespaces.Properties, GeneratedTypeToken(view),
            GeneratedTypeToken(property.DeclaringType!), property.Name);
}
