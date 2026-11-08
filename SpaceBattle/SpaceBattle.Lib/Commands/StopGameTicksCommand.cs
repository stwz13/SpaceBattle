namespace SpaceBattle.Lib;

public sealed class StopGameTicksCommand(IGameTickControl ticks) : ICommand
{
    public void Execute() => ticks.Stop();
}
