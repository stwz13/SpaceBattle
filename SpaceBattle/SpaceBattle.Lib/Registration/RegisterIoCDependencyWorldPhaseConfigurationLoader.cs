using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyWorldPhaseConfigurationLoader : ICommand
{
    private readonly IReadOnlyDictionary<WorldPhaseExecution, WorldPhaseMode> _modes;

    public RegisterIoCDependencyWorldPhaseConfigurationLoader() : this([]) { }

    public RegisterIoCDependencyWorldPhaseConfigurationLoader(IEnumerable<WorldPhaseMode> additionalModes)
    {
        ArgumentNullException.ThrowIfNull(additionalModes);
        _modes = WorldPhaseModes.With(additionalModes);
    }

    public void Execute()
    {
        Register(WorldPhaseConfigurationDependencies.Modes, _modes);
        Register(WorldPhaseConfigurationDependencies.Loader, new JsonWorldPhaseConfigurationLoader(_modes));
    }

    private static void Register(string key, object service) =>
        Ioc.Resolve<App.ICommand>("IoC.Register", key, (object[] args) =>
        {
            if (args.Length != 0)
                throw new ArgumentException("World phase configuration services take no arguments.");
            return service;
        }).Execute();
}
