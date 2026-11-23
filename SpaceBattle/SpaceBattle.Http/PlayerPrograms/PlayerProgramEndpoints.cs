using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal static class PlayerProgramEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/programs/languages", (PlayerProgramService programs) => Results.Ok(programs.Languages));
        app.MapPut("/players/me/program/{language}", Upload);
        app.MapPost("/games/{id}/program/run", Run);
    }

    private static async Task<IResult> Upload(HttpContext context, string language, IServerApplication server,
        PlayerProgramService programs, IPlayerProgramLimits limits)
    {
        var player = await ApiEndpoints.AuthenticatePlayer(context, server.Tokens);
        if (player is null) return Results.Unauthorized();
        try
        {
            var source = await BoundedProgramInput.Read(context.Request.Body, limits.SourceBytes, context.RequestAborted);
            programs.Upload(player.PlayerId, language, source);
            return Results.NoContent();
        }
        catch (PlayerProgramException) { return Results.StatusCode(StatusCodes.Status413PayloadTooLarge); }
        catch (ArgumentException error) { return Results.BadRequest(error.Message); }
    }

    private static async Task<IResult> Run(HttpContext context, string id, IServerApplication server,
        PlayerProgramService programs)
    {
        var player = await ApiEndpoints.AuthenticatePlayer(context, server.Tokens);
        if (player is null) return Results.Unauthorized();
        if (player.GameId != id) return Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            return await programs.Run(player, context.RequestAborted) ? Results.Accepted() : Results.NoContent();
        }
        catch (UnauthorizedAccessException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ServerOverloadedException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (InvalidOperationException error) { return Results.Conflict(error.Message); }
        catch (PlayerProgramException error) { return Results.UnprocessableEntity(error.Message); }
        catch (ProgramSandboxUnavailableException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (MessageFormatException error) { return Results.BadRequest(error.Message); }
    }
}
