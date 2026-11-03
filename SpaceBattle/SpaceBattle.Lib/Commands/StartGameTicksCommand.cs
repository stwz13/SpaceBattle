namespace SpaceBattle.Lib;

public sealed class StartGameTicksCommand(IGameTicks ticks) : ICommand
{
    public void Execute() => ticks.Start();
}
