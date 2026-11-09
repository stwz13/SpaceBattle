namespace SpaceBattle.Lib;

public sealed record WorldPhaseDescription(
    string Name,
    WorldPhaseExecution Execution,
    string Action,
    string? Selector);
