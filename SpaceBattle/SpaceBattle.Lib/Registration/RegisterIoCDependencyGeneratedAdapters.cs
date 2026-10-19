namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGeneratedAdapters : ICommand
{
    private readonly IReadOnlyList<Type> _interfaces;
    private readonly IReadOnlyList<Dependency> _dependencies;

    public RegisterIoCDependencyGeneratedAdapters(IEnumerable<Type> interfaces, IEnumerable<Dependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(dependencies);
        _interfaces = interfaces.ToArray();
        _dependencies = dependencies.ToArray();
    }

    public void Execute() => GeneratedStrategyRegistration.Execute(_interfaces,
        Array.Empty<(Type Command, Type Interface)>(), _dependencies);
}
