namespace SpaceBattle.Lib;

public interface IPlayerCredentialVerifier
{
    /// <summary>Returns the verified account's player ID, or null if the credentials are invalid.</summary>
    ValueTask<string?> Verify(string login, string password, CancellationToken cancellationToken);
}

public interface IPlayerGameAccess
{
    /// <summary>Checks whether the verified player may receive a token for a game, including before it starts.</summary>
    ValueTask<bool> Allows(string playerId, string gameId, CancellationToken cancellationToken);
}

public interface IPlayerTokenIssuer
{
    /// <summary>Issues a game-scoped player token without privileged permissions.</summary>
    IssuedPlayerToken Issue(string playerId, string gameId);
}

public sealed record IssuedPlayerToken(string AccessToken, int ExpiresIn);

public abstract record PlayerTokenResult
{
    public sealed record Issued(IssuedPlayerToken Token) : PlayerTokenResult;
    public sealed record InvalidCredentials : PlayerTokenResult;
    public sealed record GameForbidden : PlayerTokenResult;
}

public sealed class PlayerTokenService
{
    private readonly IPlayerCredentialVerifier _credentials;
    private readonly IPlayerGameAccess _gameAccess;
    private readonly IPlayerTokenIssuer _issuer;

    public PlayerTokenService(IPlayerCredentialVerifier credentials, IPlayerGameAccess gameAccess, IPlayerTokenIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(gameAccess);
        ArgumentNullException.ThrowIfNull(issuer);
        _credentials = credentials;
        _gameAccess = gameAccess;
        _issuer = issuer;
    }

    public async ValueTask<PlayerTokenResult> Issue(string login, string password, string gameId, CancellationToken cancellationToken)
    {
        var playerId = await _credentials.Verify(login, password, cancellationToken).ConfigureAwait(false);
        if (playerId is null) return new PlayerTokenResult.InvalidCredentials();

        if (!await _gameAccess.Allows(playerId, gameId, cancellationToken).ConfigureAwait(false))
            return new PlayerTokenResult.GameForbidden();

        cancellationToken.ThrowIfCancellationRequested();
        return new PlayerTokenResult.Issued(_issuer.Issue(playerId, gameId));
    }
}
