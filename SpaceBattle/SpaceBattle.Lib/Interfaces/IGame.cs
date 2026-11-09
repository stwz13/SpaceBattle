namespace SpaceBattle.Lib;

public interface IGame : ICommand
{
    string Id { get; }
    bool HasCommands { get; }
    IReadOnlyCollection<ICommand> Pending { get; }
    void Post(ICommand command);
}
