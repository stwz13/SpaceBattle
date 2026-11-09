namespace SpaceBattle.Lib;

public sealed class StartGameTicksCommand(IGameTickControl ticks) : ICommand
{
    public void Execute() => ticks.Start();
}
