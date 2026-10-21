namespace SpaceBattle.Lib;

public interface ICommandProducer
{
    void Add(ICommand command);
    bool TryAdd(ICommand command);
    void AddFirst(ICommand command);
    void CompleteAdding();
}
