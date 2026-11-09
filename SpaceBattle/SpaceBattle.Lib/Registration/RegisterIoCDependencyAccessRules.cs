namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyAccessRules : ICommand
{
    public void Execute()
    {
        IoCRegistrar.Register(AuthenticationDependencies.AccessRule("existing-owner"), _ => new ExistingItemOwnerRule());
        IoCRegistrar.Register(AuthenticationDependencies.AccessRule("new-owner"), _ => new NewItemOwnerRule());
    }
}
