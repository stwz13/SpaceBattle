using System.Net;
using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class HttpTransferFailureTests
{
    [Theory]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    [InlineData("invalid", HttpStatusCode.Unauthorized)]
    [InlineData("other-game", HttpStatusCode.Forbidden)]
    public async Task Unauthorized_Transfer_Does_Not_Contact_Destination(string tokenKind, HttpStatusCode expected)
    {
        await using var source = new RuntimeFixture();
        HttpApiTests.Configure(source);
        var host = source.Runtime.Start("a");
        var remote = new Mock<IRemoteServer>(MockBehavior.Strict);
        await using var factory = new GameApiFactory(source, new Dictionary<string, IRemoteServer> { ["target"] = remote.Object });
        using var client = factory.CreateClient();
        var token = tokenKind switch
        {
            "missing" => null,
            "invalid" => "invalid",
            _ => AuthenticationTests.Token("p", "other", transfer: true)
        };
        using var response = await HttpApiTests.Post(client, "{\"destination\":\"target\"}", token, "/games/a/transfer");
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(GameHostState.Running, host.State);
        remote.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"destination\":1}")]
    [InlineData("{\"destination\":null}")]
    [InlineData("{\"destination\":\"\"}")]
    [InlineData("{\"destination\":\"   \"}")]
    public async Task Invalid_Transfer_Body_Is_400_And_Leaves_Game_Running(string json)
    {
        await using var source = new RuntimeFixture();
        HttpApiTests.Configure(source);
        var host = source.Runtime.Start("a");
        var remote = new Mock<IRemoteServer>(MockBehavior.Strict);
        await using var factory = new GameApiFactory(source, new Dictionary<string, IRemoteServer> { ["target"] = remote.Object });
        using var client = factory.CreateClient();
        using var response = await HttpApiTests.Post(client, json, AuthenticationTests.Token("p", "a", transfer: true), "/games/a/transfer");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error", await response.Content.ReadAsStringAsync());
        Assert.Equal(GameHostState.Running, host.State);
        remote.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Remote_Failure_Maps_Status_And_Restores_Source(bool denied)
    {
        await using var source = new RuntimeFixture();
        HttpApiTests.Configure(source);
        var host = source.Runtime.Start("a");
        var remote = new Mock<IRemoteServer>();
        remote.Setup(value => value.Receive(It.IsAny<string>())).ThrowsAsync(
            denied ? new UnauthorizedAccessException("Not allowed by target.") : new IOException("Target unavailable."));
        await using var factory = new GameApiFactory(source, new Dictionary<string, IRemoteServer> { ["target"] = remote.Object });
        using var client = factory.CreateClient();
        using var response = await HttpApiTests.Post(client, "{\"destination\":\"target\"}",
            AuthenticationTests.Token("p", "a", transfer: true), "/games/a/transfer");
        Assert.Equal(denied ? HttpStatusCode.Forbidden : HttpStatusCode.BadGateway, response.StatusCode);
        host.Send(RuntimeFixture.Message("a", 9));
        await host.Enqueue(() => { });
        Assert.Equal(9, Assert.Single(source.Seen).Value);
        Assert.Equal(GameHostState.Running, host.State);
    }

    [Fact]
    public async Task Unknown_Destination_Is_404_Without_Suspending_Source()
    {
        await using var source = new RuntimeFixture();
        HttpApiTests.Configure(source);
        var host = source.Runtime.Start("a");
        await using var factory = new GameApiFactory(source);
        using var client = factory.CreateClient();
        using var response = await HttpApiTests.Post(client, "{\"destination\":\"unknown\"}",
            AuthenticationTests.Token("p", "a", transfer: true), "/games/a/transfer");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(GameHostState.Running, host.State);
    }
}
