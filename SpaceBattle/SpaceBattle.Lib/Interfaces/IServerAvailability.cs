namespace SpaceBattle.Lib;

public interface IServerAvailability
{
    bool IsRunning { get; }
    void EnsureRunning();
}
