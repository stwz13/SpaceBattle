using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class AuthenticationTests
{
    [Fact]
    public async Task Pending_Authenticated_Message_Preserves_Its_Player_After_Server_Restore()
    {
        await using var source = new RuntimeFixture();
        Configure(source);
        var game = source.Resolve<IGame>(GameDependencies.Create, "a");
        var host = source.Resolve<ManagedGameHost>(GameDependencies.Start, game);
        var message = source.Resolve<IMessage>("Messages.Authenticate", RuntimeFixture.Message("a", 17), new PlayerIdentity("p1", "a", false));
        // Place a real pending message on the owner thread without scheduling a quantum before the snapshot.
        await host.Execute(() => game.Post(new InterpretMessageCommand(source.Resolve<IMessageInterpreter>("Messages.Interpreter"), message)));
        var store = source.Resolve<ISnapshotStore>("Snapshots.Memory");
        await source.Runtime.SnapshotAndStop(store).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("authenticated-message", store.Load("a"));
        Assert.Contains("p1", store.Load("a"));
        await using var target = new RuntimeFixture();
        Configure(target);
        await target.Runtime.Restore(store).WaitAsync(TimeSpan.FromSeconds(10));
        await ((ManagedGameHost)target.Runtime.Find("a")).Enqueue(() => { });
        Assert.Equal(17, Assert.Single(target.Seen).Value);
        Assert.Empty(source.Seen);
        Assert.Empty(target.Errors.Handled);
    }

    public const string Secret = "integration-test-signing-key-at-least-32-bytes";
    public static string Token(string player, string game, string key = Secret, string issuer = "SpaceBattle",
        string audience = "Players", bool expired = false, bool transfer = false) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer, Audience = audience, IssuedAt = DateTime.UtcNow.AddMinutes(-10),
            NotBefore = DateTime.UtcNow.AddMinutes(-10), Expires = DateTime.UtcNow.AddMinutes(expired ? -1 : 30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object> { ["sub"] = player, ["game"] = game, ["permission"] = transfer ? "transfer" : "play" }
        });

    public static void Configure(RuntimeFixture fixture)
    {
        using var scope = fixture.Scope.Enter();
        new RegisterIoCDependencyJwtAuthentication("SpaceBattle", "Players", Secret).Execute();
    }

    [Fact]
    public async Task Valid_Token_Binds_Player_And_Game_And_Rejects_Cross_Game_Messages()
    {
        await using var fixture = new RuntimeFixture();
        Configure(fixture);
        var player = await fixture.Resolve<IPlayerTokenValidator>("Auth.Validator").Validate(Token("p1", "a"));
        Assert.Equal("p1", player.PlayerId);
        var message = fixture.Resolve<AuthenticatedMessage>("Messages.Authenticate", RuntimeFixture.Message("a", 1), player);
        Assert.Equal("p1", message.PlayerId);
        Assert.Throws<UnauthorizedAccessException>(() => fixture.Resolve<IMessage>("Messages.Authenticate", RuntimeFixture.Message("b", 1), player));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("player")]
    [InlineData("game")]
    [InlineData("malformed")]
    public async Task Invalid_Tokens_Are_Rejected(string failure)
    {
        await using var fixture = new RuntimeFixture();
        Configure(fixture);
        var token = failure == "malformed" ? "not-a-token" : Token(failure == "player" ? "" : "p",
            failure == "game" ? "" : "a", failure == "signature" ? Secret + "wrong" : Secret,
            failure == "issuer" ? "Wrong" : "SpaceBattle", failure == "audience" ? "Wrong" : "Players", failure == "expired");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Resolve<IPlayerTokenValidator>("Auth.Validator").Validate(token));
    }
}
