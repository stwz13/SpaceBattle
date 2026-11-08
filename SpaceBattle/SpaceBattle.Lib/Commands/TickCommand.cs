namespace SpaceBattle.Lib;

public sealed class TickCommand(IGameTickReceiver ticks, long generation, long number) : ICommand
{
    public void Execute() => ticks.ExecuteTick(generation, number);
}
