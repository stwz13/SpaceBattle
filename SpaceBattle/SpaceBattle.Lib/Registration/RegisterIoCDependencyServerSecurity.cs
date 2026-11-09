namespace SpaceBattle.Lib;

public sealed class RegisterIoCDependencyServerSecurity(ServerOptions options) : ICommand
{
    public void Execute()
    {
        new RegisterIoCDependencyJwtAuthentication(options.JwtIssuer, options.JwtAudience, options.JwtSecret).Execute();
        new RegisterIoCDependencyCommandWhitelist(options.AllowedCommands).Execute();
    }
}
