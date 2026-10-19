using System.Reflection;
using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGeneratedStrategiesWithAdapters : ICommand
{
    private readonly IReadOnlyList<Assembly> _assemblies;
    private readonly IReadOnlyList<Type> _interfaces;
    private readonly IReadOnlyList<Dependency> _dependencies;

    public RegisterIoCDependencyGeneratedStrategiesWithAdapters(IEnumerable<Assembly> assemblies,
        IEnumerable<Type> interfaces, IEnumerable<Dependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(dependencies);
        _assemblies = assemblies.Distinct().ToArray();
        _interfaces = interfaces.ToArray();
        _dependencies = dependencies.ToArray();
    }

    public void Execute()
    {
        var finder = Ioc.Resolve<ICommandFinder>("CodeGeneration.CommandFinder");
        var commands = _assemblies.SelectMany(finder.Find).Distinct().ToArray();
        GeneratedStrategyRegistration.Execute(_interfaces.Concat(commands.Select(command => command.Interface)),
            commands, _dependencies);
    }
}
