namespace SpaceBattle.Lib;

public interface IGameHostTransfer
{
    bool TrySend(IMessage message);
    Task<string> Begin(List<IMessage> buffer, int limit);
    void Commit(IGameArrival arrival, IRemoteServer remote);
    Task Cancel();
    IReadOnlyCollection<IMessage> Buffered { get; }
    void CompleteRelocation();
}
