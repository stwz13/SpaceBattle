using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyItemKind : ICommand
{
    private readonly string _kind;
    private readonly IReadOnlyCollection<string> _properties;

    public RegisterIoCDependencyItemKind(string kind, IEnumerable<string> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(properties);
        _kind = kind;
        _properties = properties.ToArray();
    }

    public void Execute() => Ioc.Resolve<App.ICommand>("IoC.Register", SerializationDependencies.Schema(_kind),
        (object[] args) => args.Length == 0
            ? _properties
            : throw new ArgumentException("The schema of an item kind takes no arguments.")).Execute();
}
