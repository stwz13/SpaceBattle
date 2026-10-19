using System.Reflection;

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

    public void Execute() => new RegisterIoCDependencyGeneratedStrategiesWithAdapters(
        [_assembly], [], _dependencies).Execute();
}
