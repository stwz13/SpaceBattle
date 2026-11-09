using System.Text.Json;
namespace SpaceBattle.Lib;

public interface IAccessRule { void Check(IAuthenticatedMessage message, string itemId, IItemOwners owners); }
