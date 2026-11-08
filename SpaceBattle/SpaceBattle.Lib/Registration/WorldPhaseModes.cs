using App;

namespace SpaceBattle.Lib;

internal static class WorldPhaseModes
{
    public static IReadOnlyDictionary<WorldPhaseExecution, WorldPhaseMode> With(
        IEnumerable<WorldPhaseMode>? additionalModes)
    {
        WorldPhaseMode[] defaults =
        [
            new(WorldPhaseExecution.World, false,
                (phase, frame) => Ioc.Resolve<ICommand>("Commands.Action", phase.Action, frame)),
            new(WorldPhaseExecution.Items, true,
                (phase, frame) => RegisterIoCDependencyWorldItemsAction.Create(frame, phase.Action,
                    item => Ioc.Resolve<Func<IGameItem, bool>>(phase.Selector!)(item)))
        ];
        return defaults.Concat(additionalModes ?? []).ToDictionary(mode =>
        {
            ArgumentNullException.ThrowIfNull(mode);
            ArgumentException.ThrowIfNullOrWhiteSpace(mode.Execution.Name);
            ArgumentNullException.ThrowIfNull(mode.Create);
            return mode.Execution;
        });
    }
}
