using System.Reflection;
using App;

namespace SpaceBattle.Lib;

internal static class GeneratedStrategyRegistration
{
    public static void Execute(IEnumerable<Type> interfaces, IReadOnlyList<(Type Command, Type Interface)> commands,
        IEnumerable<Dependency> dependencies)
    {
        var views = interfaces.Distinct().ToArray();
        foreach (var view in views)
        {
            if (view == null || !InterfaceInspector.IsAdaptable(view))
            {
                throw new ArgumentException($"The type {view} is not an adaptable interface.", nameof(interfaces));
            }
        }
        if (views.Length == 0 && commands.Count == 0)
        {
            return;
        }

        var properties = PropertyStrategies(views, dependencies.Distinct().ToArray());
        var adapterBuilder = Ioc.Resolve<IAdapterSourceBuilder>("CodeGeneration.AdapterSourceBuilder");
        var strategyBuilder = Ioc.Resolve<IStrategySourceBuilder>("CodeGeneration.StrategySourceBuilder");
        var compiler = Ioc.Resolve<ISourceCompiler>("CodeGeneration.SourceCompiler");
        var source = $"namespace {GeneratedNames.Namespace}\n{{\n"
            + string.Concat(views.Select(adapterBuilder.Build))
            + strategyBuilder.Build(views, commands) + "}\n";
        var references = views.SelectMany(TypeAssemblies)
            .Concat(views.SelectMany(view => view.GetInterfaces()).SelectMany(TypeAssemblies))
            .Concat(views.SelectMany(InterfaceInspector.Properties)
                .SelectMany(property => TypeAssemblies(property.PropertyType)))
            .Concat(commands.Select(command => command.Command.Assembly))
            .Append(typeof(IGameItem).Assembly)
            .Append(typeof(Ioc).Assembly)
            .Distinct();
        var strategies = compiler.Compile(source, references).GetType(GeneratedNames.StrategiesFullName)!;

        foreach (var view in views)
        {
            Register(GeneratedNames.AdapterDependency(view),
                strategies.GetMethod(GeneratedNames.AdapterMethod(view))!.CreateDelegate<Func<object[], object>>());
        }
        foreach (var (command, _) in commands)
        {
            Register(GeneratedNames.CommandDependency(command),
                strategies.GetMethod(GeneratedNames.CommandMethod(command))!.CreateDelegate<Func<object[], object>>());
        }
        foreach (var (key, strategy) in properties)
        {
            Register(key, strategy);
        }
    }

    private static IReadOnlyList<(string Key, Func<object[], object> Strategy)> PropertyStrategies(
        IEnumerable<Type> views, IReadOnlyList<Dependency> dependencies)
    {
        var strategies = new List<(string Key, Func<object[], object> Strategy)>();
        foreach (var view in views)
        {
            foreach (var property in InterfaceInspector.Properties(view))
            {
                var name = property.Name;
                var key = GeneratedNames.PropertyDependency(view, property);
                if (property.CanRead)
                {
                    var dependency = FindDependency(view, property, dependencies);
                    if (dependency == null)
                    {
                        strategies.Add((key, args => ((IGameItem)args[0]).GetProperty(name)));
                    }
                    else
                    {
                        var strategy = dependency.Strategy;
                        strategies.Add((key, args => Ioc.Resolve<object>(strategy, args)));
                    }
                }
                if (property.CanWrite)
                {
                    strategies.Add((key + ".Set", args =>
                        new SetPropertyCommand((IGameItem)args[0], name, args[1])));
                }
            }
        }
        return strategies;
    }

    private static Dependency? FindDependency(Type view, PropertyInfo property, IEnumerable<Dependency> dependencies)
    {
        var candidates = dependencies.Where(dependency => dependency.Property == property.Name
            && dependency.Interface.IsAssignableFrom(view)
            && property.DeclaringType!.IsAssignableFrom(dependency.Interface)
            && DescribesProperty(dependency.Interface, property)).ToArray();
        var mostSpecific = candidates.Where(dependency => !candidates.Any(other =>
            other.Interface != dependency.Interface && dependency.Interface.IsAssignableFrom(other.Interface))).ToArray();
        if (mostSpecific.Length > 1)
        {
            throw new InvalidOperationException($"Ambiguous dependency for {view.FullName}.{property.Name}.");
        }
        return mostSpecific.SingleOrDefault();
    }

    private static bool DescribesProperty(Type description, PropertyInfo property)
    {
        var own = description.GetProperties().Where(candidate => candidate.Name == property.Name).ToArray();
        return own.Length == 0 || own.Any(candidate => candidate.DeclaringType == property.DeclaringType);
    }

    private static IEnumerable<Assembly> TypeAssemblies(Type type) => new[] { type.Assembly }
        .Concat(type.GetGenericArguments().SelectMany(TypeAssemblies))
        .Concat(type.HasElementType ? TypeAssemblies(type.GetElementType()!) : Array.Empty<Assembly>());

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
