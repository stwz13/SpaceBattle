namespace SpaceBattle.Lib;

public sealed partial class ManagedGameHost
{
    internal IGameHostTransfer TransferControl { get; set; } = null!;
    partial void HandleUnavailable(IMessage message, ref bool handled) => handled = TransferControl.TrySend(message);
    public Task<string> BeginTransfer(List<IMessage> buffer, int limit) => TransferControl.Begin(buffer, limit);
    public void CommitTransfer(IGameArrival arrival, IRemoteServer remote) => TransferControl.Commit(arrival, remote);
    public Task CancelTransfer() => TransferControl.Cancel();
    public void Activate(IReadOnlyCollection<IMessage> messages) { _ = ActivateCore(messages); }
    internal Task ResumeBuffered(IReadOnlyCollection<IMessage> messages) => ActivateCore(messages);
    private Task ActivateCore(IReadOnlyCollection<IMessage> messages)
    {
        var commands = messages.Select(message => post(_runner, message)).ToArray();
        return invoke(_thread, () =>
        {
            Array.ForEach(commands, command => command.Execute());
            _game.Paused = false;
            wake(_runner);
        });
    }
}
