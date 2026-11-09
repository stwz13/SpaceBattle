namespace SpaceBattle.Lib;

internal sealed class ServerThreadMember(string id, IServerThread thread)
{
    public string Id { get; } = id;
    public IServerThread Thread { get; } = thread;
    public Task? Completion { get; set; }
}
