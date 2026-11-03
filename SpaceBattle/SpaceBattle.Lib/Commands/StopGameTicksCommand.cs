namespace SpaceBattle.Lib;

public sealed class StopGameTicksCommand(IGameTicks ticks) : ICommand
{
    public void Execute() => ticks.Stop();
}
