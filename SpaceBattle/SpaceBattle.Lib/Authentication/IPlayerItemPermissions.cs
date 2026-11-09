using System.Text.Json;
namespace SpaceBattle.Lib;

public interface IPlayerItemPermissions { void Check(string playerId, string itemId); }
