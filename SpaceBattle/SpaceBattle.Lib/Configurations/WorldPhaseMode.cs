namespace SpaceBattle.Lib;

public sealed record WorldPhaseMode(
    WorldPhaseExecution Execution,
    bool RequiresSelector,
    Func<WorldPhaseDescription, IGameItem, ICommand> Create);
