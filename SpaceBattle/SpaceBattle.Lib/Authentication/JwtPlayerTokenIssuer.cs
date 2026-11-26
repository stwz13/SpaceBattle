using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SpaceBattle.Lib;

public sealed class JwtPlayerTokenIssuer : IPlayerTokenIssuer
{
    private const int LifetimeSeconds = 300;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeProvider _clock;
    private readonly SigningCredentials _signing;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtPlayerTokenIssuer(string issuer, string audience, string secret, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(clock);
        var key = Encoding.UTF8.GetBytes(secret);
        if (key.Length < 32) throw new ArgumentException("The signing key must contain at least 32 UTF-8 bytes.", nameof(secret));
        _issuer = issuer;
        _audience = audience;
        _clock = clock;
        _signing = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
    }

    public IssuedPlayerToken Issue(string playerId, string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        var now = _clock.GetUtcNow().UtcDateTime;
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddSeconds(LifetimeSeconds),
            SigningCredentials = _signing,
            Claims = new Dictionary<string, object> { ["sub"] = playerId, ["game"] = gameId }
        });
        return new IssuedPlayerToken(token, LifetimeSeconds);
    }
}
