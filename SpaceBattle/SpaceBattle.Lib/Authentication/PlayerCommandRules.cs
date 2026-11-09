using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class PlayerCommandRules(IPlayerCommandPermissions permissions, ITargetItemResolver targets,
    IPlayerItemPermissions items,
    IReadOnlyDictionary<string, IReadOnlyList<IAccessRule>> commandRules) : IPlayerCommandRules
{
    public void Validate(IAuthenticatedMessage message, IItemOwners owners)
    {
        permissions.Check(message);
        if (!commandRules.TryGetValue(message.Command, out var rules))
            throw new UnauthorizedAccessException("The command has no access rules.");
        var itemId = targets.Resolve(message);
        items.Check(message.PlayerId, itemId);
        foreach (var rule in rules) rule.Check(message, itemId, owners);
    }
}
