using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SpaceBattle.Lib;

public sealed record PlayerIdentity(string PlayerId, string GameId, bool CanTransfer);

public interface IPlayerTokenValidator { Task<PlayerIdentity> Validate(string token); }

public sealed class JwtPlayerTokenValidator(JsonWebTokenHandler handler, TokenValidationParameters parameters,
    Func<System.Security.Claims.ClaimsIdentity, PlayerIdentity> identity) : IPlayerTokenValidator
{
    public async Task<PlayerIdentity> Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("A bearer token is required.");
        var result = await handler.ValidateTokenAsync(token, parameters).ConfigureAwait(false);
        if (!result.IsValid) throw new UnauthorizedAccessException("Invalid bearer token.");
        return identity(result.ClaimsIdentity);
    }
}

public sealed class AuthenticatedMessage(IMessage message, string playerId) : IAuthenticatedMessage
{
    public string PlayerId { get; } = playerId;
    public string GameId => message.GameId;
    public string Command => message.Command;
    public JsonElement Args => message.Args;
}
