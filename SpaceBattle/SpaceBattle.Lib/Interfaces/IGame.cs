namespace SpaceBattle.Lib;

public interface IGame : ICommand
{
    string Id { get; }
    bool HasCommands { get; }
    void Post(ICommand command);
}
