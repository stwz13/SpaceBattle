using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyConfiguredWorldPhases : ICommand
{
    private readonly string _path;
    private readonly string _action;

    public RegisterIoCDependencyConfiguredWorldPhases(string path,
        string action = WorldStepDependencies.DefaultAction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        _path = path;
        _action = action;
    }

    public void Execute()
    {
        var phases = Ioc.Resolve<IWorldPhaseConfigurationLoader>(
            WorldPhaseConfigurationDependencies.Loader).Load(_path);
        var modes = Ioc.Resolve<IReadOnlyDictionary<WorldPhaseExecution, WorldPhaseMode>>(
            WorldPhaseConfigurationDependencies.Modes);
        var configKey = _action + ".Config";
        var names = new HashSet<string>(StringComparer.Ordinal);
        var bindings = phases.Select(phase =>
        {
            if (string.IsNullOrWhiteSpace(phase.Name) || string.IsNullOrWhiteSpace(phase.Action))
                throw new FormatException("World phases require a name and an action.");
            if (phase.Name == configKey || phase.Name == "Commands.Action" || phase.Name == "Commands.Macro"
                || phase.Action == _action)
                throw new FormatException("World phases cannot replace composition dependencies or invoke their enclosing action.");
            if (!names.Add(phase.Name))
                throw new FormatException($"Duplicate world phase '{phase.Name}'.");
            if (!modes.TryGetValue(phase.Execution, out var mode))
                throw new FormatException($"Unknown execution mode '{phase.Execution.Name}' in phase '{phase.Name}'.");
            if (mode.RequiresSelector ? string.IsNullOrWhiteSpace(phase.Selector) : phase.Selector is not null)
                throw new FormatException($"Invalid selector for phase '{phase.Name}'.");

            return (Key: WorldStepDependencies.Phase(_action, phase.Name), Phase: phase, Mode: mode);
        }).ToArray();

        foreach (var (key, phase, mode) in bindings)
        {
            Register(key, args =>
            {
                if (args.Length != 1 || args[0] is not IGameItem frame)
                    throw new ArgumentException("A world phase requires one world step frame.");
                return mode.Create(phase, frame);
            });
        }
        var keys = bindings.Select(binding => binding.Key).ToArray();
        Register(configKey, args =>
        {
            if (args.Length != 0)
                throw new ArgumentException("World phase configuration takes no arguments.");
            return keys.ToArray();
        });
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
