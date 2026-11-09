using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class ExistingItemOwnerRule : IAccessRule
{
    public void Check(IAuthenticatedMessage message, string itemId, IItemOwners owners)
    {
        if (!Equals(owners.OwnerOf(itemId), message.PlayerId))
            throw new UnauthorizedAccessException("The item must belong to the authenticated player.");
    }
}
