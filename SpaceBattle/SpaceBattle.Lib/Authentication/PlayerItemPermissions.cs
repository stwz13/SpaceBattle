using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class PlayerItemPermissions(IReadOnlyDictionary<string, IReadOnlySet<string>> items) : IPlayerItemPermissions
{
    public void Check(string playerId, string itemId)
    {
        if (!items.TryGetValue(playerId, out var allowed) || (!allowed.Contains(itemId) && !allowed.Contains("*")))
            throw new UnauthorizedAccessException("The player cannot access this item.");
    }
}
