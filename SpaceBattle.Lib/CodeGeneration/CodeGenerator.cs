using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SpaceBattle.Lib;

/// <summary>
/// Reflection → текст C# → компиляция (Roslyn).
/// Используется только при подготовке инфраструктуры; готовые стратегии Reflection не используют.
/// </summary>
public static class CodeGenerator
{
    public const string Namespace = "SpaceBattle.Generated";
    public const string StrategiesClass = Namespace + ".Strategies";

    private static readonly ConcurrentDictionary<string, Assembly> _compiled = new();

    // ---------- Имена (правила из практики №2) ----------

    public static string Mangle(Type type) =>
        type.FullName!.Replace('.', '_').Replace('+', '_');

    public static string AdapterClassName(Type iface) => Mangle(iface) + "Adapter";

    public static string AdapterDependency(Type iface) => "Adapters." + AdapterClassName(iface);

    public static string CommandDependency(Type command) => "Commands." + Mangle(command);

    public static string AdapterMethod(Type iface) => "Adapter_" + AdapterClassName(iface);

    public static string CommandMethod(Type command) => "Command_" + Mangle(command);



    public static IReadOnlyList<PropertyInfo> Properties(Type iface) =>
        iface.GetProperties()
            .Concat(iface.GetInterfaces().SelectMany(i => i.GetProperties()))
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToList();

    /// <summary>Адаптер можно сгенерировать, если интерфейс состоит только из обычных свойств.</summary>
    public static bool IsAdaptable(Type type)
    {
        if (!type.IsInterface || type.IsGenericType)
        {
            return false;
        }

        var all = new[] { type }.Concat(type.GetInterfaces()).ToList();
        var hasMethods = all.SelectMany(i => i.GetMethods()).Any(m => !m.IsSpecialName);
        var hasEvents = all.SelectMany(i => i.GetEvents()).Any();
        var hasIndexers = Properties(type).Any(p => p.GetIndexParameters().Length > 0);

        return !hasMethods && !hasEvents && !hasIndexers;
    }

    public static IReadOnlyList<(Type Command, Type Interface)> FindCommands(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
            .Where(t => typeof(ICommand).IsAssignableFrom(t) && t.Name.EndsWith("Command"))
            .Select(t => (Command: t, Interface: t.GetConstructors()
                .Select(c => c.GetParameters())
                .Where(p => p.Length == 1 && IsAdaptable(p[0].ParameterType))
                .Select(p => p[0].ParameterType)
                .FirstOrDefault()))
            .Where(x => x.Interface != null)
            .Select(x => (x.Command, x.Interface!))
            .ToList();



    public static string TypeName(Type t)
    {
        if (t.IsArray)
        {
            return TypeName(t.GetElementType()!) + "[" + string.Concat(Enumerable.Repeat(",", t.GetArrayRank() - 1)) + "]";
        }

        if (t.IsGenericType)
        {
            var name = t.GetGenericTypeDefinition().FullName!;
            name = name[..name.IndexOf('`')];
            return "global::" + name.Replace('+', '.') + "<" + string.Join(", ", t.GetGenericArguments().Select(TypeName)) + ">";
        }

        return "global::" + t.FullName!.Replace('+', '.');
    }

    public static string GenerateAdapterSource(Type iface)
    {
        var item = TypeName(typeof(IGameItem));
        var sb = new StringBuilder();

        sb.AppendLine($"    public class {AdapterClassName(iface)} : {TypeName(iface)}");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {item} _item;");
        sb.AppendLine();
        sb.AppendLine($"        public {AdapterClassName(iface)}({item} item)");
        sb.AppendLine("        {");
        sb.AppendLine("            _item = item;");
        sb.AppendLine("        }");

        foreach (var p in Properties(iface))
        {
            var type = TypeName(p.PropertyType);

            sb.AppendLine();
            sb.AppendLine($"        public {type} {p.Name}");
            sb.AppendLine("        {");
            if (p.CanRead)
            {
                sb.AppendLine($"            get {{ return global::App.Ioc.Resolve<{type}>(\"Properties.{p.Name}\", _item); }}");
            }
            if (p.CanWrite)
            {
                sb.AppendLine($"            set {{ global::App.Ioc.Resolve<{TypeName(typeof(ICommand))}>(\"Properties.{p.Name}.Set\", _item, value).Execute(); }}");
            }
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        return sb.ToString();
    }

    public static string GenerateSource(IEnumerable<Type> interfaces, IEnumerable<(Type Command, Type Interface)> commands)
    {
        var ifaces = interfaces.ToList();
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {Namespace}");
        sb.AppendLine("{");

        foreach (var iface in ifaces)
        {
            sb.Append(GenerateAdapterSource(iface));
            sb.AppendLine();
        }

        sb.AppendLine("    public static class Strategies");
        sb.AppendLine("    {");

        foreach (var iface in ifaces)
        {
            sb.AppendLine($"        public static object {AdapterMethod(iface)}(object[] args)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {AdapterClassName(iface)}(({TypeName(typeof(IGameItem))})args[0]);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        foreach (var (command, iface) in commands)
        {
            sb.AppendLine($"        public static object {CommandMethod(command)}(object[] args)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {TypeName(command)}(global::App.Ioc.Resolve<{TypeName(iface)}>(\"{AdapterDependency(iface)}\", args));");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static Assembly Compile(string source, IEnumerable<Assembly> references)
    {
        return _compiled.GetOrAdd(source, s =>
        {
            var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            var paths = references.Select(a => a.Location)
                .Concat(trusted)
                .Where(p => !string.IsNullOrEmpty(p))
                .GroupBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());

            var compilation = CSharpCompilation.Create(
                Namespace + "_" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(s) },
                paths.Select(p => MetadataReference.CreateFromFile(p)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);

            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
                throw new InvalidOperationException("Generated code does not compile:\n" + string.Join("\n", errors));
            }

            return Assembly.Load(stream.ToArray());
        });
    }

    public static Func<object[], object> Strategy(Assembly compiled, string method) =>
        compiled.GetType(StrategiesClass)!
            .GetMethod(method)!
            .CreateDelegate<Func<object[], object>>();
}
