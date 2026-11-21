using System.Text.Json;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

public static class GameRuleEndpoints
{
    public static void Map(WebApplication app)
    {
        var routes = app.MapGroup("/games/{id}/rules");
        routes.MapPost("/lobby", (HttpContext context, string id, IGameRuleRooms rooms, IServerApplication server) =>
            Handle(context, id, server, async player =>
            {
                var roster = await Body<string[]>(context);
                if (server.Runtime.Games.Any(game => game.Id == id))
                    return Results.Conflict("The game is already running.");
                rooms.Create(id, player, roster);
                return Results.StatusCode(StatusCodes.Status201Created);
            }));
        routes.MapPut("", (HttpContext context, string id, IGameRuleRooms rooms, IServerApplication server) =>
            Handle(context, id, server, async player =>
            {
                using var document = await JsonDocument.ParseAsync(context.Request.Body,
                    cancellationToken: context.RequestAborted);
                var rules = GameRulesJson.Read(document.RootElement);
                return Results.Ok(new { revision = rooms.Find(id).Propose(player, rules) });
            }));
        routes.MapGet("", (HttpContext context, string id, IGameRuleRooms rooms, IServerApplication server) =>
            Handle(context, id, server, player => Task.FromResult<IResult>(Results.Ok(rooms.Find(id).ForParticipant(player)))));
        routes.MapPost("/{revision:long}/confirm", (HttpContext context, string id, long revision,
            IGameRuleRooms rooms, IServerApplication server) => Handle(context, id, server, player =>
            {
                rooms.Find(id).Confirm(player, revision);
                return Task.FromResult<IResult>(Results.NoContent());
            }));
        routes.MapPost("/{revision:long}/start", (HttpContext context, string id, long revision,
            IGameRuleRooms rooms, IServerApplication server) => Handle(context, id, server, player =>
            {
                rooms.Start(id, player, revision);
                return Task.FromResult<IResult>(Results.NoContent());
            }));
    }

    private static async Task<T> Body<T>(HttpContext context) where T : class =>
        await JsonSerializer.DeserializeAsync<T>(context.Request.Body, cancellationToken: context.RequestAborted)
            ?? throw new JsonException("Expected a JSON value.");

    private static async Task<IResult> Handle(HttpContext context, string id, IServerApplication server,
        Func<string, Task<IResult>> action)
    {
        var identity = await ApiEndpoints.AuthenticatePlayer(context, server.Tokens);
        if (identity is null) return Results.Unauthorized();
        if (identity.GameId != id) return Results.StatusCode(StatusCodes.Status403Forbidden);
        try { return await action(identity.PlayerId); }
        catch (UnauthorizedAccessException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        catch (KeyNotFoundException error) { return Results.NotFound(error.Message); }
        catch (ServerOverloadedException) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        catch (InvalidOperationException error) { return Results.Conflict(error.Message); }
        catch (ArgumentException error) { return Results.BadRequest(error.Message); }
        catch (JsonException error) { return Results.BadRequest(error.Message); }
    }
}
