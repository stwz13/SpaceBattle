namespace SpaceBattle.Lib;

public static class CSharpTypeName
{
    public static string Format(Type t)
    {
        if (t.IsArray)
        {
            return Format(t.GetElementType()!) + "[" + string.Concat(Enumerable.Repeat(",", t.GetArrayRank() - 1)) + "]";
        }

        var arguments = t.GetGenericArguments();
        var used = 0;
        var parts = new List<string>();
        foreach (var part in DeclaringTypes(t))
        {
            var name = part.Name.Split('`')[0];
            var count = part.GetGenericArguments().Length - (part.DeclaringType?.GetGenericArguments().Length ?? 0);
            if (count > 0)
            {
                name += "<" + string.Join(", ", arguments.Skip(used).Take(count).Select(Format)) + ">";
                used += count;
            }
            parts.Add(name);
        }
        return (string.IsNullOrEmpty(t.Namespace) ? "" : t.Namespace + ".") + string.Join(".", parts);
    }

    private static IEnumerable<Type> DeclaringTypes(Type type) => type.DeclaringType == null
        ? new[] { type }
        : DeclaringTypes(type.DeclaringType).Append(type);
}
