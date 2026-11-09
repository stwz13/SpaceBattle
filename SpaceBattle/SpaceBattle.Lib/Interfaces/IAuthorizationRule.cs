namespace SpaceBattle.Lib;

public interface IAuthorizationRule
{
    AuthorizationDecision Evaluate(string subject, string action, IGameItem target);
}
