using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SpaceBattle.Lib;

namespace SpaceBattle.Http;

public sealed record HttpGameServices(IPlayerTokenValidator Tokens,
    Func<PlayerIdentity, IMessageReceiver> Receiver, Func<string, string, Task> Transfer);

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/messages", async (HttpContext context, [FromServices] HttpGameServices services) =>
        {
            var player = await Authenticate(context, services.Tokens);
            if (player is null) return Results.Unauthorized();
            return await Dispatch(context, async () =>
            {
                using var reader = new StreamReader(context.Request.Body);
                services.Receiver(player).Receive(await reader.ReadToEndAsync(context.RequestAborted));
                return Results.Accepted();
            });
        });
        app.MapPost("/games/{id}/transfer", async (string id, HttpContext context, [FromServices] HttpGameServices services) =>
        {
            var player = await Authenticate(context, services.Tokens);
            if (player is null) return Results.Unauthorized();
            if (player.GameId != id || !player.CanTransfer) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await Dispatch(context, async () =>
            {
                using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                if (body.RootElement.ValueKind != JsonValueKind.Object
                    || !body.RootElement.TryGetProperty("destination", out var destination)
                    || destination.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(destination.GetString()))
                    throw new MessageFormatException("A destination name is required.");
                await services.Transfer(id, destination.GetString()!);
                return Results.NoContent();
            });
        });
    }

    private static async Task<PlayerIdentity?> Authenticate(HttpContext context, IPlayerTokenValidator tokens)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        try { return await tokens.Validate(header[7..]); }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static async Task<IResult> Dispatch(HttpContext context, Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (ServerOverloadedException)
        {
            context.Response.Headers.RetryAfter = "1";
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (MessageFormatException error) { return Results.BadRequest(new { error = error.Message }); }
        catch (JsonException) { return Results.BadRequest(new { error = "Invalid JSON." }); }
        catch (UnauthorizedAccessException) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (IOException) { return Results.StatusCode(StatusCodes.Status502BadGateway); }
    }
}
