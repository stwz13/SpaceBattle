using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class PlayerCommandPermissions(IReadOnlyDictionary<string, IReadOnlySet<string>> commands) : IPlayerCommandPermissions
{
    public void Check(IAuthenticatedMessage message)
    {
        if (!commands.TryGetValue(message.PlayerId, out var allowed) || !allowed.Contains(message.Command))
            throw new UnauthorizedAccessException("The player cannot use this command.");
    }
}
