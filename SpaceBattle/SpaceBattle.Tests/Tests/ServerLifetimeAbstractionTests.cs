using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class ServerLifetimeAbstractionTests
{
    [Fact]
    public async Task Host_Uses_Injected_Application_Without_Constructing_Default_Server()
    {
        var application = new Mock<IServerApplication>();
        var tokens = new Mock<IPlayerTokenValidator>();
        var receiver = new Mock<IMessageReceiver>();
        application.SetupGet(value => value.Tokens).Returns(tokens.Object);
        application.Setup(value => value.Receiver(It.IsAny<PlayerIdentity>())).Returns(receiver.Object);
        application.Setup(value => value.Start()).Returns(Task.CompletedTask);
        application.Setup(value => value.Stop()).Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddSingleton(application.Object);
        HostComposition.Configure(services, new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var http = provider.GetRequiredService<HttpGameServices>();
        Assert.Same(tokens.Object, http.Tokens);
        Assert.Same(receiver.Object, http.Receiver(new PlayerIdentity("p", "a", false)));
        var lifetime = new ServerLifetime(provider.GetRequiredService<IServerApplication>());
        await lifetime.StartAsync(CancellationToken.None);
        await lifetime.StopAsync(CancellationToken.None);
        application.Verify(value => value.Start(), Times.Once);
        application.Verify(value => value.Stop(), Times.Once);
    }
}
