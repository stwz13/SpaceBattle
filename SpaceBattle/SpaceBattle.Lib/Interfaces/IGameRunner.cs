namespace SpaceBattle.Lib;

public interface IGameRunner : ICommand
{
    void Post(ICommand command);
}
