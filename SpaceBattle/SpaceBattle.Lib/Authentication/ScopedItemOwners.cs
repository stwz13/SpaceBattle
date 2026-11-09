using System.Text.Json;
namespace SpaceBattle.Lib;

public sealed class ScopedItemOwners(Func<IGameItemStorage> storage) : IItemOwners
{
    public object? OwnerOf(string id)
    {
        try { return storage().Get(id).GetProperty("Owner"); }
        catch (KeyNotFoundException) { return null; }
    }
}
