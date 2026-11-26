using Microsoft.Extensions.DependencyInjection.Extensions;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal static class PlayerTokenComposition
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("PlayerTokenIssuance");
        services.TryAddSingleton<IPlayerCredentialVerifier>(_ => new ConfiguredPlayerCredentialVerifier(
            settings.GetSection("Accounts").Get<Dictionary<string, PlayerAccountOptions>>() ?? []));
        services.TryAddSingleton<IPlayerGameAccess>(_ => new ConfiguredPlayerGameAccess(
            settings.GetSection("GamesByPlayer").Get<Dictionary<string, string[]>>() ?? []));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPlayerTokenIssuer>(provider =>
        {
            var options = provider.GetRequiredService<ServerOptions>();
            return new JwtPlayerTokenIssuer(options.JwtIssuer, options.JwtAudience, options.JwtSecret,
                provider.GetRequiredService<TimeProvider>());
        });
        services.TryAddSingleton<PlayerTokenService>();
    }
}
