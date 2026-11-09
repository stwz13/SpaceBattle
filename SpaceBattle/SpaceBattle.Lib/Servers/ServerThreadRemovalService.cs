namespace SpaceBattle.Lib;

internal sealed class ServerThreadRemovalService(IServerThreadMembership members,
    Func<IServerThread, ICommand> softStop)
{
    public async Task Remove(string id, Func<IServerThread, Task> evacuate)
    {
        ArgumentNullException.ThrowIfNull(evacuate);
        var member = members.BeginRemoval(id);
        try
        {
            try { await evacuate(member.Thread).ConfigureAwait(false); }
            catch { members.RollbackRemoval(member); throw; }
            member.Thread.Add(softStop(member.Thread));
            await member.Completion!.ConfigureAwait(false);
            members.CompleteRemoval(member);
        }
        finally { members.EndRemoval(); }
    }
}
