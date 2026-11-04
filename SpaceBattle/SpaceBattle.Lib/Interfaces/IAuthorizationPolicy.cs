namespace SpaceBattle.Lib;

public interface IAuthorizationPolicy
{
    bool IsAllowed(string subject, string action, IGameItem target);
}
