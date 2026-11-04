namespace SpaceBattle.Lib;

public interface IAuthorizedActionFactory
{
    ICommand Create(string subject, string action, IGameItem target);
}
