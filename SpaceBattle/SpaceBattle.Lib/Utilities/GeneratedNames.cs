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

    public static string AdapterClass(Type iface) => Identifier("", iface, "Adapter");

    public static string AdapterDependency(Type iface) => DependencyKey(DependencyNamespaces.Adapters, iface, "Adapter");

    public static string CommandDependency(Type command) => DependencyKey(DependencyNamespaces.Commands, command);

    public static string AdapterMethod(Type iface) => Identifier("Adapter", iface, "Adapter");

    public static string CommandMethod(Type command) => Identifier("Command", command);

    public static string PropertyDependency(Type view, PropertyInfo property) =>
        DependencyKey(DependencyNamespaces.Properties, Mangle(view), Mangle(property.DeclaringType!), property.Name);
}
