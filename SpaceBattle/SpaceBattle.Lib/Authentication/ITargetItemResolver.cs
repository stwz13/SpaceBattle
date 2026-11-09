using System.Text.Json;
namespace SpaceBattle.Lib;

public interface ITargetItemResolver { string Resolve(IAuthenticatedMessage message); }
