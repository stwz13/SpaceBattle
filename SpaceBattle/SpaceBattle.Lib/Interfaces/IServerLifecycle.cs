namespace SpaceBattle.Lib;

public interface IServerLifecycle
{
    void Start();
    Task SoftStop();
    Task HardStop();
}
