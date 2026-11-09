using System.Reflection;

namespace SpaceBattle.Lib;

public static class InterfaceInspector
{
    public static IReadOnlyList<PropertyInfo> Properties(Type iface) =>
        iface.GetProperties()
            .Concat(iface.GetInterfaces().SelectMany(i => i.GetProperties()))
            .GroupBy(p => (p.DeclaringType, p.Name))
            .Select(g => g.First())
            .ToList();

    public static bool IsAdaptable(Type type)
    {
        if (!type.IsInterface || !type.IsVisible || type.IsGenericType)
        {
            return false;
        }

        var all = new[] { type }.Concat(type.GetInterfaces()).ToList();
        var hasMethods = all.SelectMany(i => i.GetMethods()).Any(m => !m.IsSpecialName);
        var hasEvents = all.SelectMany(i => i.GetEvents()).Any();
        var hasIndexers = Properties(type).Any(p => p.GetIndexParameters().Length > 0);

        return !hasMethods && !hasEvents && !hasIndexers;
    }
}
