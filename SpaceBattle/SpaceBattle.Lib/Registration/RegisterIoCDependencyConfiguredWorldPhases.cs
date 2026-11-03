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
        var configKey = _action + ".Config";
        if (phases.Any(phase => phase.Name == configKey
            || phase.Name == "Commands.Action" || phase.Name == "Commands.Macro"
            || phase.Action == _action))
            throw new FormatException("World phases cannot replace composition dependencies or invoke their enclosing action.");

        foreach (var phase in phases)
        {
            if (phase.Execution == WorldPhaseExecution.Items)
            {
                new RegisterIoCDependencyWorldItemsAction(phase.Name, phase.Action,
                    item => Ioc.Resolve<Func<IGameItem, bool>>(phase.Selector!)(item)).Execute();
            }
            else
            {
                Register(phase.Name, args =>
                {
                    if (args.Length != 1 || args[0] is not IGameItem frame)
                        throw new ArgumentException("A world phase requires one world step frame.");
                    return Ioc.Resolve<ICommand>("Commands.Action", phase.Action, frame);
                });
            }
        }
        var names = phases.Select(phase => phase.Name).ToArray();
        Register(configKey, args =>
        {
            if (args.Length != 0)
                throw new ArgumentException("World phase configuration takes no arguments.");
            return names.ToArray();
        });
    }

    private static void Register(string key, Func<object[], object> strategy) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, strategy).Execute();
}
