using System.Reflection;

namespace SpaceBattle.Lib;

public static class GeneratedNames
{
    public const string Namespace = "SpaceBattle.Generated";
    public const string StrategiesClass = "Strategies";
    public const string StrategiesFullName = Namespace + "." + StrategiesClass;

    public static string Mangle(Type type) => type.FullName!.Replace('.', '_').Replace('+', '_');

    public static string AdapterClass(Type iface) => Mangle(iface) + "Adapter";

    public static string AdapterDependency(Type iface) => "Adapters." + AdapterClass(iface);

    public static string CommandDependency(Type command) => "Commands." + Mangle(command);

    public static string AdapterMethod(Type iface) => "Adapter_" + AdapterClass(iface);

    public static string CommandMethod(Type command) => "Command_" + Mangle(command);

    public static string PropertyDependency(Type view, PropertyInfo property) =>
        $"Properties.{Mangle(view)}.{Mangle(property.DeclaringType!)}.{property.Name}";
}
