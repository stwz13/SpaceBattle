namespace SpaceBattle.Lib;

internal sealed class RegisterIoCDependencyFleetAccess : ICommand
{
    private readonly IReadOnlyList<string> _participants;
    private readonly IReadOnlyCollection<string> _actions;

    public RegisterIoCDependencyFleetAccess(IReadOnlyList<string> participants, IReadOnlyCollection<string> actions)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(actions);
        _participants = participants;
        _actions = actions;
    }

    public void Execute()
    {
        var tree = new AccessAreaTree();
        var areas = new FleetObjectAccessAreas(_participants.ToHashSet(StringComparer.Ordinal));
        var grants = new AccessGrantStore();
        const string battle = "battle";
        tree.Add(battle);
        var actions = new[] { "Movement", "Rotation", "Shooting" }.Concat(_actions).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var player in _participants)
        {
            var fleet = FleetObjectAccessAreas.For(player);
            tree.Add(fleet, battle);
            grants.Grant(player, FleetPermissions.ReadShipState, fleet);
            foreach (var action in actions)
                grants.Grant(player, action, fleet);
        }

        var rule = new InheritedGrantRule(tree, areas, grants);
        new RegisterIoCDependencyAuthorizedActions(new AuthorizationPolicy([rule])).Execute();
    }
}
