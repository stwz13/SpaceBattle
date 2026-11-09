namespace SpaceBattle.Lib;

public sealed class DeliverGameTickCommand(
    Action<ICommand> postToGame, ICommand tick, Action<Exception>? onDeliveryFailed = null)
    : ICommand, ICommandDeliveryFailureHandler
{
    public void Execute()
    {
        try
        {
            postToGame(tick);
        }
        catch (Exception error)
        {
            OnDeliveryFailed(error);
            throw;
        }
    }

    public void OnDeliveryFailed(Exception error) => onDeliveryFailed?.Invoke(error);
}
