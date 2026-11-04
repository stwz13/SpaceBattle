namespace SpaceBattle.Lib;

public sealed class AccessGrantStore : IAccessGrants
{
    private readonly HashSet<(string Subject, string Action, string Scope)> _grants = new();

    public void Grant(string subject, string action, string scope)
    {
        Validate(subject, action, scope);
        _grants.Add((subject, action, scope));
    }

    public void Revoke(string subject, string action, string scope)
    {
        Validate(subject, action, scope);
        _grants.Remove((subject, action, scope));
    }

    public bool Contains(string subject, string action, string scope)
    {
        Validate(subject, action, scope);
        return _grants.Contains((subject, action, scope));
    }

    private static void Validate(string subject, string action, string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
    }
}
