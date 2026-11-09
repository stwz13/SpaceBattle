namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyServerPolicies(ServerOptions options, ILog output) : ICommand
{
    public void Execute()
    {
        new RegisterIoCDependencyPlayerAuthorization(options.CommandTargets, options.Players,
            options.CommandAccessRules.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyList<IAccessRule>)pair.Value.Select(name => App.Ioc.Resolve<IAccessRule>(AuthenticationDependencies.AccessRule(name))).ToArray())).Execute();
        new RegisterIoCDependencyErrorPolicy(new Dictionary<Type, (ErrorAction, int)>(), output).Execute();
        new RegisterIoCDependencyThreadErrorHandling(output).Execute();
    }
}
