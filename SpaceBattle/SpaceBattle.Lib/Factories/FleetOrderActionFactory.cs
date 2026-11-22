namespace SpaceBattle.Lib;

internal sealed class FleetOrderActionFactory : IFleetOrderActionFactory
{
    private readonly IReadOnlyDictionary<string, FleetContextOrder> _schemas;
    private readonly IGameItemStorage _items;
    private readonly IAuthorizedActionFactory _single;
    private readonly IAuthorizationPolicy _policy;
    private readonly IPlayerItemPermissions _permissions;
    private readonly Func<string, ICommandContext, ICommand> _compose;

    public FleetOrderActionFactory(IReadOnlyDictionary<string, FleetContextOrder> schemas, IGameItemStorage items,
        IAuthorizedActionFactory single, IAuthorizationPolicy policy, IPlayerItemPermissions permissions,
        Func<string, ICommandContext, ICommand> compose)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(single);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(compose);
        _schemas = schemas;
        _items = items;
        _single = single;
        _policy = policy;
        _permissions = permissions;
        _compose = compose;
    }

    public ICommand Create(string player, string action, string actor, IReadOnlyDictionary<string, string> roles)
    {
        if (!_schemas.TryGetValue(action, out var schema))
        {
            if (roles.Count != 0)
                throw new MessageFormatException($"Action '{action}' has no additional roles.");
            return _single.Create(player, action, OperationalActor(actor));
        }

        if (roles.Count != schema.OtherRoles.Count || schema.OtherRoles.Any(role => !roles.ContainsKey(role)))
            throw new MessageFormatException($"Action '{action}' expects these additional roles: {string.Join(", ", schema.OtherRoles)}.");

        var participants = new Dictionary<string, IGameItem>(StringComparer.Ordinal)
        {
            [schema.ActorRole] = OperationalActor(actor)
        };
        foreach (var role in schema.OtherRoles)
        {
            var id = roles[role];
            _permissions.Check(player, id);
            participants.Add(role, _items.Get(id));
        }

        return new AuthorizedActionCommand(
            () => participants.Values.All(item => _policy.IsAllowed(player, action, item)),
            () => _compose(action, new CommandContext(participants)),
            () => throw new UnauthorizedAccessException("The requested action is not permitted for all participants."));
    }

    private IGameItem OperationalActor(string id)
    {
        var item = _items.Get(id);
        if (BattleItemProperties.IsDestroyed(item))
            throw new InvalidOperationException("The ship is out of action.");
        return item;
    }
}
