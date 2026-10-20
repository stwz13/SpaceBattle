namespace SpaceBattle.Lib;

public interface ICommandContext
{
    IGameItem GetItem(string role);
}
