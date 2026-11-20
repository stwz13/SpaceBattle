using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyRuleCommand : ICommand
{
    private readonly string _name;
    private readonly Func<object[], ICommand> _create;

    public string Name => _name;

    public RegisterIoCDependencyRuleCommand(string name, Func<object[], ICommand> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(create);
        _name = name;
        _create = create;
    }

    public static RegisterIoCDependencyRuleCommand FromExisting(string key) =>
        new(key, args => Ioc.Resolve<ICommand>(key, args));

    public static RegisterIoCDependencyRuleCommand For<TInput, TTarget>(string name,
        Func<TInput, TTarget> adapt, IRuleCommandFactory<TTarget> factory)
    {
        ArgumentNullException.ThrowIfNull(adapt);
        ArgumentNullException.ThrowIfNull(factory);
        return new RegisterIoCDependencyRuleCommand(name, args => args is [TInput input]
            ? factory.Create(adapt(input))
            : throw new ArgumentException($"Command '{name}' expects one {typeof(TInput).Name}."));
    }

    internal static string Key(string name) => $"GameRules.Steps.{name}.Create";

    private static string FactoryKey(string name) => $"GameRules.Steps.{name}.Factory";

    public void Execute() => IoCRegistrar.Register(FactoryKey(_name), _ => _create);

    internal static void Bind(string name) => IoCRegistrar.Register(Key(name), args =>
    {
        Func<object[], ICommand> create;
        try { create = Ioc.Resolve<Func<object[], ICommand>>(FactoryKey(name)); }
        catch (Exception error)
        {
            throw new KeyNotFoundException($"No implementation is registered for game rule command '{name}'.", error);
        }
        return create(args);
    });
}
