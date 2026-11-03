using Microsoft.Extensions.DependencyInjection.Extensions;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

public static class HostComposition
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(_ => configuration.GetSection("Server").Get<ServerOptions>() ?? new ServerOptions());
        services.TryAddSingleton(provider => ServerComposition.Create(provider.GetRequiredService<ServerOptions>(),
            log: new ApplicationLog(provider.GetRequiredService<ILogger<ApplicationLog>>())));
        services.TryAddSingleton<IServerApplication>(provider => provider.GetRequiredService<ServerApplication>());
        services.TryAddSingleton<IReadOnlyDictionary<string, IRemoteServer>>(_ => new Dictionary<string, IRemoteServer>());
        services.TryAddSingleton(provider =>
        {
            var application = provider.GetRequiredService<IServerApplication>();
            var targets = provider.GetRequiredService<IReadOnlyDictionary<string, IRemoteServer>>();
            return new HttpGameServices(application.Tokens, application.Receiver,
                (id, destination) => targets.TryGetValue(destination, out var target)
                    ? application.Runtime.Transfer(id, target) : throw new KeyNotFoundException("Unknown destination."));
        });
        services.AddHostedService<ServerLifetime>();
    }
}

public sealed class ApplicationLog(ILogger<ApplicationLog> logger) : ILog
{
    public void Write(string gameId, Exception error, ErrorAction action, int attempt) =>
        logger.LogError(error, "Game {GameId}: {ErrorAction}, attempt {Attempt}", gameId, action, attempt);
}

public sealed class ServerLifetime(IServerApplication application) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => application.Start();
    public Task StopAsync(CancellationToken cancellationToken) => application.Stop().WaitAsync(cancellationToken);
}
