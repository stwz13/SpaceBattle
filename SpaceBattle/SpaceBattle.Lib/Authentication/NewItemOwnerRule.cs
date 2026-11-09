using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class NewItemOwnerRule : IAccessRule
{
    public void Check(IAuthenticatedMessage message, string itemId, IItemOwners owners)
    {
        if (OwnerClaim.From(message.Args) != message.PlayerId)
            throw new UnauthorizedAccessException("A new item must belong to the authenticated player.");
    }
}
