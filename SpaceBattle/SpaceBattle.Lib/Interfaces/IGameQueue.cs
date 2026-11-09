namespace SpaceBattle.Lib;

public interface IGameQueue
{
    bool HasCommands { get; }
    IReadOnlyCollection<ICommand> Pending { get; }
    void Enqueue(ICommand command);
    ICommand Dequeue();
}
