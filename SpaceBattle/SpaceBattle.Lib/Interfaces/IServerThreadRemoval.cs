namespace SpaceBattle.Lib;

public interface IServerThreadRemoval
{
    Task RemoveThread(string id, Func<IServerThread, Task> evacuate);
}
