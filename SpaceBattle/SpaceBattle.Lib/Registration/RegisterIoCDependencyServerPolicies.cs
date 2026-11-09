namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyServerPolicies(ServerOptions options, ILog output) : ICommand
{
    public void Execute()
    {
        new RegisterIoCDependencyErrorPolicy(new Dictionary<Type, (ErrorAction, int)>(), output).Execute();
        new RegisterIoCDependencyThreadErrorHandling(output).Execute();
    }
}
