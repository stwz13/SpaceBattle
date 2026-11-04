namespace SpaceBattle.Lib;

public sealed class AuthorizationPolicy : IAuthorizationPolicy
{
    private readonly IAuthorizationRule[] _rules;

    public AuthorizationPolicy(IEnumerable<IAuthorizationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToArray();
        if (_rules.Any(rule => rule is null))
            throw new ArgumentException("Authorization rules cannot contain null.", nameof(rules));
    }

    public bool IsAllowed(string subject, string action, IGameItem target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(target);
        var allowed = false;
        foreach (var rule in _rules)
        {
            var decision = rule.Evaluate(subject, action, target);
            if (decision != AuthorizationDecision.Allow && decision != AuthorizationDecision.Abstain)
                return false;
            allowed |= decision == AuthorizationDecision.Allow;
        }
        return allowed;
    }
}
