namespace SpaceBattle.Lib;

public sealed class DeliverGameTickCommand(Action<ICommand> postToGame, ICommand tick) : ICommand
{
    public void Execute() => postToGame(tick);
}
