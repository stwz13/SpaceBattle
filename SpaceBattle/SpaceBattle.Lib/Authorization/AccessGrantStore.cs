namespace SpaceBattle.Lib;

public sealed class AccessGrantStore : IAccessGrants
{
    private readonly HashSet<(string Subject, string Action, string Area)> _grants = new();

    public void Grant(string subject, string action, string area)
    {
        Validate(subject, action, area);
        _grants.Add((subject, action, area));
    }

    public void Revoke(string subject, string action, string area)
    {
        Validate(subject, action, area);
        _grants.Remove((subject, action, area));
    }

    public bool Contains(string subject, string action, string area)
    {
        Validate(subject, action, area);
        return _grants.Contains((subject, action, area));
    }

    private static void Validate(string subject, string action, string area)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
    }
}
