namespace SpaceBattle.Lib;

public interface IServerThread
{
    int ThreadId { get; }
    Task Start();
    void Add(ICommand command);
    bool TryAdd(ICommand command);
    void Interrupt(ICommand command);
    void Schedule(ICommand command);
    void UpdateBehaviour(Action behaviour);
    void HardStop();
    void SoftStop();
}
