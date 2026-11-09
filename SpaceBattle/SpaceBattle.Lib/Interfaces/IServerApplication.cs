namespace SpaceBattle.Lib;

public interface IServerApplication
{
    IGameRuntime Runtime { get; }
    IPlayerTokenValidator Tokens { get; }
    ISnapshotStore Store { get; }
    IMessageReceiver Receiver(PlayerIdentity player);
    Task Start();
    Task Stop();
}
