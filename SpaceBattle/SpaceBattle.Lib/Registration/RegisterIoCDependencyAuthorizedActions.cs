using App;

namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyAuthorizedActions : ICommand
{
    private readonly IAuthorizationPolicy _policy;

    public RegisterIoCDependencyAuthorizedActions(IAuthorizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    public void Execute()
    {
        Ioc.Resolve<App.ICommand>("IoC.Register", AuthorizationDependencies.Policy, (object[] args) =>
        {
            if (args.Length != 0)
                throw new ArgumentException("No arguments are expected.");
            return _policy;
        }).Execute();

        Ioc.Resolve<App.ICommand>("IoC.Register", AuthorizationDependencies.Factory, (object[] args) =>
        {
            if (args.Length != 0)
                throw new ArgumentException("No arguments are expected.");
            return new AuthorizedActionFactory(
                Ioc.Resolve<IAuthorizationPolicy>(AuthorizationDependencies.Policy),
                (action, item) => Ioc.Resolve<ICommand>("Commands.Action", action, item));
        }).Execute();

        Ioc.Resolve<App.ICommand>("IoC.Register", AuthorizationDependencies.Command, (object[] args) =>
        {
            if (args.Length != 3 || args[0] is not string subject ||
                args[1] is not string action || args[2] is not IGameItem target)
                throw new ArgumentException("Expected a trusted subject, action and target item.");
            return Ioc.Resolve<IAuthorizedActionFactory>(AuthorizationDependencies.Factory)
                .Create(subject, action, target);
        }).Execute();
    }
}
