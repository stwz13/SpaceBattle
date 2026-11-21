using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal static class BattleResultEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/games/{id}/result", async (HttpContext context, string id, IServerApplication server) =>
        {
            var player = await ApiEndpoints.AuthenticatePlayer(context, server.Tokens);
            if (player is null) return Results.Unauthorized();
            if (player.GameId != id) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (server.Runtime.Games.FirstOrDefault(game => game.Id == id) is not { } host)
                return Results.NotFound();
            BattleProgress? result = null;
            await host.Execute(() => result = BattleSimulationModule.ReadProgress());
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
