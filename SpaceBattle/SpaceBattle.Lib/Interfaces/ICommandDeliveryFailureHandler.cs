namespace SpaceBattle.Lib;

public interface ICommandDeliveryFailureHandler
{
    /// <summary>Reports rejection from a delivery thread. The handler must not throw.</summary>
    void OnDeliveryFailed(Exception error);
}
