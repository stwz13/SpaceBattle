namespace SpaceBattle.Lib;

public sealed partial class GameRuntime : IRemoteServer
{
    internal IGameTransferService Transfers { get; set; } = null!;
    public void Send(IMessage message) => Transfers.Send(message);
    public Task Transfer(string id, IRemoteServer destination) => Transfers.Transfer(id, destination);
    public Task<IGameArrival> Receive(string snapshot) => Transfers.Receive(snapshot);
}
