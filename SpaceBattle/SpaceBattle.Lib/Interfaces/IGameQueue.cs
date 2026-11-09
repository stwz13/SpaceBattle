namespace SpaceBattle.Lib;

public interface IGameQueue
{
    bool HasCommands { get; }
    void Enqueue(ICommand command);
    ICommand Dequeue();
}
