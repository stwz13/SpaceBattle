using System.Text.Json;
namespace SpaceBattle.Lib;

public interface IPlayerCommandPermissions { void Check(IAuthenticatedMessage message); }
