using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal static class PlayerTokenEndpoints
{
    public static void Map(WebApplication app) => app.MapPost("/auth/player-token", Issue);

    private static async Task<IResult> Issue(PlayerTokenRequest request, PlayerTokenService tokens, HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Game))
            return Results.BadRequest(new { error = "Login, password and game are required." });

        var result = await tokens.Issue(request.Login, request.Password, request.Game, context.RequestAborted);
        return result switch
        {
            PlayerTokenResult.Issued issued => Ok(issued.Token, context),
            PlayerTokenResult.InvalidCredentials => Results.Unauthorized(),
            PlayerTokenResult.GameForbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            _ => throw new InvalidOperationException("Unexpected token issuance result.")
        };
    }

    private static IResult Ok(IssuedPlayerToken token, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { accessToken = token.AccessToken, tokenType = "Bearer", expiresIn = token.ExpiresIn });
    }

    private sealed record PlayerTokenRequest(string? Login, string? Password, string? Game);
}
