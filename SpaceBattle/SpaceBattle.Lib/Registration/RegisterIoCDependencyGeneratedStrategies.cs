using System.Reflection;
using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGeneratedStrategies : ICommand
{
    private readonly Assembly _assembly;
    private readonly IReadOnlyList<Dependency> _dependencies;

    public RegisterIoCDependencyGeneratedStrategies(Assembly assembly, IEnumerable<Dependency> dependencies)
    {
        _assembly = assembly;
        _dependencies = dependencies.ToList();
    }

    public void Execute()
    {
        var finder = Ioc.Resolve<ICommandFinder>("CodeGeneration.CommandFinder");
        var adapterBuilder = Ioc.Resolve<IAdapterSourceBuilder>("CodeGeneration.AdapterSourceBuilder");
        var strategyBuilder = Ioc.Resolve<IStrategySourceBuilder>("CodeGeneration.StrategySourceBuilder");
        var compiler = Ioc.Resolve<ISourceCompiler>("CodeGeneration.SourceCompiler");

        var commands = finder.Find(_assembly);
        var interfaces = commands.Select(c => c.Interface).Distinct().ToList();

        var source =
            $"namespace {GeneratedNames.Namespace}\n{{\n" +
            string.Concat(interfaces.Select(adapterBuilder.Build)) +
            strategyBuilder.Build(interfaces, commands) +
            "}\n";

        var references = interfaces.Select(i => i.Assembly)
            .Concat(interfaces.SelectMany(i => i.GetInterfaces()).Select(i => i.Assembly))
            .Concat(commands.Select(c => c.Command.Assembly))
            .Concat(interfaces.SelectMany(InterfaceInspector.Properties)
                .SelectMany(p => TypeAssemblies(p.PropertyType)))
            .Append(typeof(IGameItem).Assembly)
            .Append(typeof(Ioc).Assembly)
            .Distinct();

        var strategies = compiler.Compile(source, references).GetType(GeneratedNames.StrategiesFullName)!;

        foreach (var iface in interfaces)
        {
            Register(GeneratedNames.AdapterDependency(iface), Strategy(strategies, GeneratedNames.AdapterMethod(iface)));
        }

        foreach (var (command, _) in commands)
        {
            Register(GeneratedNames.CommandDependency(command), Strategy(strategies, GeneratedNames.CommandMethod(command)));
        }

        RegisterProperties(interfaces);
    }

    private void RegisterProperties(IEnumerable<Type> interfaces)
    {
        foreach (var iface in interfaces)
        {
            foreach (var property in InterfaceInspector.Properties(iface))
            {
                var name = property.Name;
                var key = GeneratedNames.PropertyDependency(iface, property);
                if (property.CanRead)
                {
                    var dependency = FindDependency(iface, property);
                    if (dependency != null)
                    {
                        var strategy = dependency.Strategy;
                        Register(key, args => Ioc.Resolve<object>(strategy, args));
                    }
                    else
                    {
                        Register(key, args => ((IGameItem)args[0]).GetProperty(name));
                    }
                }
                if (property.CanWrite)
                {
                    Register(key + ".Set", args =>
                        new SetPropertyCommand((IGameItem)args[0], name, args[1]));
                }
            }
        }
    }

    private Dependency? FindDependency(Type view, PropertyInfo property)
    {
        var candidates = _dependencies.Where(d => d.Property == property.Name
            && d.Interface.IsAssignableFrom(view)
            && property.DeclaringType!.IsAssignableFrom(d.Interface)
            && DescribesProperty(d.Interface, property)).ToList();
        var mostSpecific = candidates.Where(d => !candidates.Any(other =>
            other.Interface != d.Interface && d.Interface.IsAssignableFrom(other.Interface))).ToList();
        if (mostSpecific.Count > 1)
        {
            throw new InvalidOperationException($"Ambiguous dependency for {view.FullName}.{property.Name}.");
        }
        return mostSpecific.SingleOrDefault();
    }

    private static bool DescribesProperty(Type description, PropertyInfo property)
    {
        var own = description.GetProperties().Where(p => p.Name == property.Name).ToList();
        return own.Count == 0 || own.Any(p => p.DeclaringType == property.DeclaringType);
    }

    private static IEnumerable<Assembly> TypeAssemblies(Type type) =>
        new[] { type.Assembly }
            .Concat(type.GetGenericArguments().SelectMany(TypeAssemblies))
            .Concat(type.HasElementType ? TypeAssemblies(type.GetElementType()!) : Array.Empty<Assembly>());

    private static Func<object[], object> Strategy(Type strategies, string method) =>
        strategies.GetMethod(method)!.CreateDelegate<Func<object[], object>>();

    private static void Register(string name, Func<object[], object> strategy)
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", name, strategy).Execute();
    }
}
