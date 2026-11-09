using System.Text.Json;
namespace SpaceBattle.Lib;

public interface IPlayerCommandRules { void Validate(IAuthenticatedMessage message, IItemOwners owners); }
