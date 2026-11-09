namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyServerRuntime(ServerOptions options) : ICommand
{
    public void Execute()
    {
        new RegisterIoCDependencyGameSnapshot().Execute();
        new RegisterIoCDependencyGameItems().Execute();
        options.ItemKinds.ToList().ForEach(kind => new RegisterIoCDependencyItemKind(kind.Key, kind.Value).Execute());
        new RegisterIoCDependencyManagedGames().Execute();
        new RegisterIoCDependencyGameTransfer(options.InboxCapacity).Execute();
        new RegisterIoCDependencyRebalancing().Execute();
        new RegisterIoCDependencyThreadRemoval().Execute();
    }
}
