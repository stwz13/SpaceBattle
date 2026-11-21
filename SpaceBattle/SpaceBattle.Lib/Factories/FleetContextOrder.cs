namespace SpaceBattle.Lib;

public sealed class FleetContextOrder
{
    public FleetContextOrder(string action, string actorRole, IEnumerable<string> otherRoles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorRole);
        ArgumentNullException.ThrowIfNull(otherRoles);
        var roles = otherRoles.ToArray();
        if (roles.Any(string.IsNullOrWhiteSpace) ||
            roles.Append(actorRole).Distinct(StringComparer.Ordinal).Count() != roles.Length + 1)
            throw new ArgumentException("The order requires distinct, nonempty role names.", nameof(otherRoles));

        Action = action;
        ActorRole = actorRole;
        OtherRoles = Array.AsReadOnly(roles);
    }

    public string Action { get; }
    public string ActorRole { get; }
    public IReadOnlyList<string> OtherRoles { get; }
}
