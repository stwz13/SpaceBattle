using App;
using System.Text.Json;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyRuleCommand : ICommand
{
    private readonly string _name;
    private readonly Func<object[], ICommand> _create;

    public string Name => _name;
    public IGameRuleParameter? Parameter { get; private init; }
    internal const string RuleValuesDependency = "GameRules.ParameterValues";

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

    public static RegisterIoCDependencyRuleCommand For<TInput, TTarget, TSettings>(string name,
        Func<TInput, TTarget> adapt, IConfiguredRuleCommandFactory<TTarget, TSettings> factory,
        Func<JsonElement, TSettings> read, TSettings defaults)
    {
        ArgumentNullException.ThrowIfNull(adapt);
        ArgumentNullException.ThrowIfNull(factory);
        var parameter = new GameClassParameter<TSettings>(GameRuleParameterNames.ForCommand(name), read, _ => { });
        return new RegisterIoCDependencyRuleCommand(name, args =>
        {
            if (args is not [TInput input])
                throw new ArgumentException($"Command '{name}' expects one {typeof(TInput).Name}.");
            var values = Ioc.Resolve<IReadOnlyDictionary<string, JsonElement>>(RuleValuesDependency);
            var settings = values.TryGetValue(parameter.Name, out var value) ? parameter.Decode(value) : defaults;
            return factory.Create(adapt(input), settings);
        }) { Parameter = parameter };
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
