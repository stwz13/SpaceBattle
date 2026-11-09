namespace SpaceBattle.Lib;

public sealed class InheritedGrantRule : IAuthorizationRule
{
    private readonly IAccessAreaTree _tree;
    private readonly IObjectAccessAreas _objects;
    private readonly IAccessGrants _grants;

    public InheritedGrantRule(IAccessAreaTree tree, IObjectAccessAreas objects, IAccessGrants grants)
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
        var area = _objects.FindArea(target);
        return area is not null && _tree.GetPath(area).Any(node => _grants.Contains(subject, action, node))
            ? AuthorizationDecision.Allow
            : AuthorizationDecision.Abstain;
    }
}
