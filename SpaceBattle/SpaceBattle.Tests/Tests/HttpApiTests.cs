using System.Net;
using System.Net.Http.Headers;
using System.Text;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class HttpApiTests
{
    public static void Configure(RuntimeFixture fixture)
    {
        GameTransferTests.Configure(fixture);
        AuthenticationTests.Configure(fixture);
        using var scope = fixture.Scope.Enter();
        new RegisterIoCDependencyCommandWhitelist(["Record", "CreateItem"]).Execute();
    }
    public static Task<HttpResponseMessage> Post(HttpClient client, string text, string? token = null, string path = "/messages")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    [Theory]
    [InlineData("none", "a", "Record", HttpStatusCode.Unauthorized)]
    [InlineData("invalid", "a", "Record", HttpStatusCode.Unauthorized)]
    [InlineData("valid", "b", "Record", HttpStatusCode.Forbidden)]
    [InlineData("valid", "a", "Unknown", HttpStatusCode.BadRequest)]
    [InlineData("missing", "missing", "Record", HttpStatusCode.NotFound)]
    [InlineData("valid", "a", "Record", HttpStatusCode.Accepted)]
    public async Task Endpoint_Validates_Token_Game_Command_And_Routes_Message(string tokenKind, string game, string command, HttpStatusCode expected)
    {
        await using var fixture = new RuntimeFixture();
        Configure(fixture);
        var host = fixture.Runtime.Start("a");
        await using var factory = new GameApiFactory(fixture);
        using var client = factory.CreateClient();
        var token = tokenKind switch { "none" => null, "invalid" => "bad", "missing" => AuthenticationTests.Token("p", "missing"), _ => AuthenticationTests.Token("p", "a") };
        using var response = await Post(client, $$"""{"gameId":"{{game}}","command":"{{command}}","args":7}""", token);
        Assert.Equal(expected, response.StatusCode);
        await host.Enqueue(() => { });
        Assert.Equal(expected == HttpStatusCode.Accepted ? 1 : 0, fixture.Seen.Count);
    }

    [Fact]
    public async Task Malformed_Json_Is_400_And_A_Full_Inbox_Is_503_With_RetryAfter()
    {
        await using var fixture = new RuntimeFixture(1, 1);
        Configure(fixture);
        var host = fixture.Runtime.Start("a");
        await using var factory = new GameApiFactory(fixture);
        using var client = factory.CreateClient();
        var token = AuthenticationTests.Token("p", "a");
        using var invalid = await Post(client, "{", token);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = host.Execute(() => { entered.SetResult(); release.Wait(TimeSpan.FromSeconds(10)); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var first = await Post(client, "{\"gameId\":\"a\",\"command\":\"Record\",\"args\":1}", token);
            using var overloaded = await Post(client, "{\"gameId\":\"a\",\"command\":\"Record\",\"args\":2}", token);
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, overloaded.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(1), overloaded.Headers.RetryAfter!.Delta);
        }
        finally { release.Set(); }
        await blocked;
    }

    [Fact]
    public async Task Transfer_Endpoint_Requires_Transfer_Permission_And_Uses_Configured_Target()
    {
        await using var source = new RuntimeFixture();
        await using var target = new RuntimeFixture();
        Configure(source); Configure(target);
        source.Runtime.Start("a");
        await using var factory = new GameApiFactory(source, new Dictionary<string, IRemoteServer> { ["target"] = target.Runtime });
        using var client = factory.CreateClient();
        using var denied = await Post(client, "{\"destination\":\"target\"}", AuthenticationTests.Token("p", "a"), "/games/a/transfer");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var moved = await Post(client, "{\"destination\":\"target\"}", AuthenticationTests.Token("p", "a", transfer: true), "/games/a/transfer");
        Assert.Equal(HttpStatusCode.NoContent, moved.StatusCode);
        Assert.NotNull(target.Runtime.Find("a"));
    }
}
