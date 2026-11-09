namespace SpaceBattle.Lib;

public readonly record struct WorldPhaseExecution(string Name)
{
    public static WorldPhaseExecution World { get; } = new("world");
    public static WorldPhaseExecution Items { get; } = new("items");
}
