using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class GameApiFactory(RuntimeFixture fixture, IReadOnlyDictionary<string, IRemoteServer>? targets = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services => services.AddSingleton(new HttpGameServices(
            fixture.Resolve<IPlayerTokenValidator>("Auth.Validator"),
            player => new AuthenticatedMessageReceiver(fixture.Resolve<IMessageParser>(MessageDependencies.Parser), fixture.Runtime,
                fixture.Resolve<ICommandWhitelist>("Messages.Whitelist"), player,
                (message, identity) => fixture.Resolve<IMessage>("Messages.Authenticate", message, identity)),
            (id, target) => targets is not null && targets.TryGetValue(target, out var remote)
                ? fixture.Runtime.Transfer(id, remote) : throw new KeyNotFoundException("Unknown destination."))));
    }
}
