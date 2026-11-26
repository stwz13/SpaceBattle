using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using SpaceBattle.Http;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public sealed class PlayerTokenHttpTests
{
    private const string IssuePath = "/auth/player-token";
    private const string Game = "jwt-match";
    private const string Rules = "/games/" + Game + "/rules";
    private const string FirstPassword = "alice's local test passphrase";
    private const string SecondPassword = "bob's local test passphrase";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Issued_Tokens_Start_A_Lobby_And_Execute_A_Fleet_Order_As_Their_Own_Player()
    {
        await using var host = new Factory();
        using var first = host.CreateClient();
        using var second = host.CreateClient();
        var application = host.Services.GetRequiredService<IServerApplication>();
        Assert.DoesNotContain(application.Runtime.Games, game => game.Id == Game);

        var firstToken = await SignIn(first, new
        {
            login = "alice", password = FirstPassword, game = Game, sub = "p2", permission = "transfer"
        });
        var secondToken = await SignIn(second, new { login = "bob", password = SecondPassword, game = Game });
        Assert.Equal(new PlayerIdentity("p1", Game, false), await application.Tokens.Validate(firstToken));
        Assert.Equal(new PlayerIdentity("p2", Game, false), await application.Tokens.Validate(secondToken));
        var jwt = new JsonWebToken(firstToken);
        Assert.Equal("HS256", jwt.Alg);
        Assert.Equal("JwtIssuanceTestIssuer", jwt.Issuer);
        Assert.Equal("JwtIssuanceTestAudience", Assert.Single(jwt.Audiences));
        Assert.Equal(TimeSpan.FromMinutes(5), jwt.ValidTo - jwt.IssuedAt);
        Assert.InRange(jwt.ValidTo - DateTime.UtcNow, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));

        using var foreignLobby = await first.PostAsJsonAsync("/games/other/rules/lobby", new[] { "p1" });
        Assert.Equal(HttpStatusCode.Forbidden, foreignLobby.StatusCode);
        using var transfer = await first.PostAsJsonAsync("/games/" + Game + "/transfer", new { destination = "elsewhere" });
        Assert.Equal(HttpStatusCode.Forbidden, transfer.StatusCode);

        using var lobby = await first.PostAsJsonAsync(Rules + "/lobby", new[] { "p1", "p2" });
        Assert.Equal(HttpStatusCode.Created, lobby.StatusCode);
        using var proposal = await first.PutAsJsonAsync(Rules, new
        {
            parameters = new Dictionary<string, object> { ["objects.Ship"] = new { turnSteps = 2 } }
        });
        Assert.Equal(HttpStatusCode.OK, proposal.StatusCode);
        using var firstConfirm = await first.PostAsync(Rules + "/1/confirm", null);
        Assert.Equal(HttpStatusCode.NoContent, firstConfirm.StatusCode);
        using var prematureStart = await first.PostAsync(Rules + "/1/start", null);
        Assert.Equal(HttpStatusCode.Conflict, prematureStart.StatusCode);
        using var secondConfirm = await second.PostAsync(Rules + "/1/confirm", null);
        Assert.Equal(HttpStatusCode.NoContent, secondConfirm.StatusCode);
        using var start = await first.PostAsync(Rules + "/1/start", null);
        Assert.Equal(HttpStatusCode.NoContent, start.StatusCode);

        using var foreignCommand = await Send(first, "other", "CreateItem", Ship("foreign", "p1"));
        Assert.Equal(HttpStatusCode.Forbidden, foreignCommand.StatusCode);
        using var firstShip = await Send(first, Game, "CreateItem", Ship("s1", "p1"));
        using var secondShip = await Send(second, Game, "CreateItem", Ship("s2", "p2"));
        Assert.Equal(HttpStatusCode.Accepted, firstShip.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, secondShip.StatusCode);

        using var firstOrder = await Send(first, Game, "FleetOrder", new { id = "s1", action = "Rotation" });
        using var spoofedOrder = await Send(first, Game, "FleetOrder", new { id = "s2", action = "Rotation", playerId = "p2" });
        Assert.Equal(HttpStatusCode.Accepted, firstOrder.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, spoofedOrder.StatusCode);
        var game = Assert.IsAssignableFrom<IManagedGameHost>(application.Runtime.Find(Game));
        await game.Enqueue(() =>
        {
            var items = Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);
            Assert.Equal(new Angle(2), items.Get("s1").GetProperty("Direction"));
            Assert.Equal(new Angle(0), items.Get("s2").GetProperty("Direction"));
        }).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Issuance_Distinguishes_Bad_Requests_Invalid_Credentials_And_Forbidden_Games()
    {
        await using var host = new Factory();
        using var client = host.CreateClient();
        using var badRequest = await client.PostAsJsonAsync(IssuePath, new { login = "alice", game = Game });
        using var wrongPassword = await client.PostAsJsonAsync(IssuePath, new { login = "alice", password = SecondPassword, game = Game });
        using var noAccount = await client.PostAsJsonAsync(IssuePath, new { login = "p1", password = FirstPassword, game = Game });
        using var forbidden = await client.PostAsJsonAsync(IssuePath, new { login = "alice", password = FirstPassword, game = "other" });
        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noAccount.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Empty(await wrongPassword.Content.ReadAsStringAsync());
        Assert.Empty(await noAccount.Content.ReadAsStringAsync());
        Assert.Empty(await forbidden.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_Configured_Accounts_Prevent_Issuance()
    {
        await using var host = new Factory(configureAccounts: false);
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync(IssuePath,
            new { login = "alice", password = FirstPassword, game = Game });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Replacing_The_Credential_Verifier_Keeps_Game_Access_And_Token_Validation()
    {
        await using var original = new Factory();
        await using var host = original.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IPlayerCredentialVerifier, ExternalCredentials>()));
        using var client = host.CreateClient();
        using var configuredAccount = await client.PostAsJsonAsync(IssuePath,
            new { login = "alice", password = FirstPassword, game = Game });
        using var forbidden = await client.PostAsJsonAsync(IssuePath,
            new { login = "external", password = FirstPassword, game = "other" });
        Assert.Equal(HttpStatusCode.Unauthorized, configuredAccount.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var token = await SignIn(client, new { login = "external", password = FirstPassword, game = Game });
        var application = host.Services.GetRequiredService<IServerApplication>();
        Assert.Equal(new PlayerIdentity("p2", Game, false), await application.Tokens.Validate(token));
    }

    private static async Task<string> SignIn(HttpClient client, object request)
    {
        using var response = await client.PostAsJsonAsync(IssuePath, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var credentials = Assert.IsType<TokenResponse>(await response.Content.ReadFromJsonAsync<TokenResponse>());
        Assert.Equal("Bearer", credentials.TokenType);
        Assert.Equal(300, credentials.ExpiresIn);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(credentials.TokenType, credentials.AccessToken);
        return credentials.AccessToken;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string gameId, string command, object args) =>
        client.PostAsJsonAsync("/messages", new { gameId, command, args });

    private static object Ship(string id, string owner) => new
    {
        id, kind = "Ship",
        properties = new Dictionary<string, object>
        {
            ["Owner"] = new { type = "string", value = owner },
            ["Position"] = new { type = "vector", value = new[] { 0, 0 } },
            ["Direction"] = new { type = "angle", value = 0 }
        }
    };

    private sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

    private sealed class ExternalCredentials : IPlayerCredentialVerifier
    {
        public ValueTask<string?> Verify(string login, string password, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(login == "external" && password == FirstPassword ? "p2" : null);
    }

    private sealed class Factory(bool configureAccounts = true) : WebApplicationFactory<Program>
    {
        private static readonly string FirstHash = Hash(FirstPassword);
        private static readonly string SecondHash = Hash(SecondPassword);

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Server:JwtIssuer"] = "JwtIssuanceTestIssuer",
                    ["Server:JwtAudience"] = "JwtIssuanceTestAudience",
                    ["Server:JwtSecret"] = AuthenticationTests.Secret,
                    ["Server:RestoreOnStart"] = "false",
                    ["Server:SnapshotOnStop"] = "false"
                };
                foreach (var player in new[] { "p1", "p2" })
                {
                    settings[$"Server:Players:{player}:Commands:0"] = "CreateItem";
                    settings[$"Server:Players:{player}:Commands:1"] = "FleetOrder";
                    settings[$"Server:Players:{player}:Items:0"] = "*";
                }
                if (configureAccounts)
                {
                    settings["PlayerTokenIssuance:Accounts:alice:PlayerId"] = "p1";
                    settings["PlayerTokenIssuance:Accounts:alice:PasswordHash"] = FirstHash;
                    settings["PlayerTokenIssuance:Accounts:bob:PlayerId"] = "p2";
                    settings["PlayerTokenIssuance:Accounts:bob:PasswordHash"] = SecondHash;
                    settings["PlayerTokenIssuance:GamesByPlayer:p1:0"] = Game;
                    settings["PlayerTokenIssuance:GamesByPlayer:p2:0"] = Game;
                }
                configuration.AddInMemoryCollection(settings);
            });

        private static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);
            return Convert.ToBase64String([.. salt, .. hash]);
        }
    }
}
