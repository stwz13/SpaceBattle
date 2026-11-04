namespace SpaceBattle.Lib;

public sealed class InheritedGrantRule : IAuthorizationRule
{
    private readonly IAccessScopeTree _tree;
    private readonly IObjectAccessScopes _objects;
    private readonly IAccessGrants _grants;

    public InheritedGrantRule(IAccessScopeTree tree, IObjectAccessScopes objects, IAccessGrants grants)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(grants);
        _tree = tree;
        _objects = objects;
        _grants = grants;
    }

    public AuthorizationDecision Evaluate(string subject, string action, IGameItem target)
    {
        var scope = _objects.FindScope(target);
        return scope is not null && _tree.GetPath(scope).Any(node => _grants.Contains(subject, action, node))
            ? AuthorizationDecision.Allow
            : AuthorizationDecision.Abstain;
    }
}
