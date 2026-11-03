namespace SpaceBattle.Lib;

public interface IGameTransfer
{
    Task Transfer(string id, IRemoteServer destination);
}
public partial interface IGameRuntime : IRemoteServer, IGameTransfer { }
public partial interface IManagedGameHost
{
    Task<string> BeginTransfer(List<IMessage> buffer, int limit);
    void CommitTransfer(IGameArrival arrival, IRemoteServer remote);
    Task CancelTransfer();
    void Activate(IReadOnlyCollection<IMessage> messages);
}
