namespace SpaceBattle.Lib;

public interface IServer : IServerLifecycle, IServerThreads, IServerAvailability, IServerThreadRemoval, IServerThreadControl, IServerThreadCatalog
{
}
