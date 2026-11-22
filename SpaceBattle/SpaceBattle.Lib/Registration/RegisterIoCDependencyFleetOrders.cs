using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyFleetOrders : ICommand
{
    private readonly IReadOnlyDictionary<string, FleetContextOrder> _contextActions;

    public RegisterIoCDependencyFleetOrders(IEnumerable<FleetContextOrder> contextActions)
    {
        ArgumentNullException.ThrowIfNull(contextActions);
        _contextActions = contextActions.ToDictionary(action => action.Action, StringComparer.Ordinal);
    }

    public void Execute()
    {
        new RegisterIoCDependencyAuthorizedActions(new AuthorizationPolicy([])).Execute();
        IoCRegistrar.Register(MessageDependencies.CommandKey(FleetOrderCommand.Name), args =>
            args is [IAuthenticatedMessage message]
                ? new FleetOrderCommand(message, new FleetOrderActionFactory(_contextActions,
                    Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage),
                    Ioc.Resolve<IAuthorizedActionFactory>(AuthorizationDependencies.Factory),
                    Ioc.Resolve<IAuthorizationPolicy>(AuthorizationDependencies.Policy),
                    Ioc.Resolve<IPlayerItemPermissions>(AuthenticationDependencies.ItemPermissions),
                    (action, context) => Ioc.Resolve<ICommand>(CommandContextDependencies.Action, action, context)))
                : throw new UnauthorizedAccessException("A fleet order requires an authenticated player."));
    }
}
