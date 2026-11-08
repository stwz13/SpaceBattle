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
        _dependencies = dependencies.ToArray();
    }

    public void Execute()
    {
        var finder = Ioc.Resolve<ICommandFinder>("CodeGeneration.CommandFinder");
        var commands = finder.Find(_assembly);
        GeneratedStrategyRegistration.Execute(commands.Select(command => command.Interface), commands, _dependencies);
    }
}
