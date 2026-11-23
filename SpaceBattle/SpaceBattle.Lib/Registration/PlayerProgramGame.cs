using App;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceBattle.Lib;

public sealed class PlayerProgramGame(IServerApplication server, IBattleShipClassifier fleet) : IPlayerProgramGame
{
    private static readonly JsonSerializerOptions MessageJson = new(JsonSerializerOptions.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ProgramTurn> ReadTurn(PlayerIdentity player, CancellationToken cancellationToken)
    {
        if (server.Runtime.Find(player.GameId) is not IManagedGameHost host)
            throw new KeyNotFoundException("The game is unavailable.");
        if (host.State != GameHostState.Running)
            throw new InvalidOperationException("The game is not running.");

        ProgramShip[] ships = [];
        await host.Execute(() =>
        {
            if (BattleSimulationModule.ReadProgress()?.IsFinished == true)
                throw new InvalidOperationException("The battle has already finished.");

            var store = Ioc.Resolve<IGameItemStorage>(GameItemStorageDependencies.Storage);
            var access = Ioc.Resolve<IAuthorizationPolicy>(AuthorizationDependencies.Policy);
            ships = store.GetSnapshot().Where(item => fleet.IsShip(item) && Property(item, "Destroyed") is not true &&
                    access.IsAllowed(player.PlayerId, "Movement", item))
                .Select(ReadShip).OrderBy(ship => ship.Id, StringComparer.Ordinal).ToArray();
        }).WaitAsync(cancellationToken);

        if (ships.Length == 0) throw new UnauthorizedAccessException("The player has no active ships in this game.");
        return new ProgramTurn(player.GameId, player.PlayerId, ships);
    }

    public void SendOrder(PlayerIdentity player, ProgramOrder order) =>
        server.Receiver(player).Receive(JsonSerializer.Serialize(new
        {
            gameId = player.GameId,
            command = FleetOrderCommand.Name,
            args = order
        }, MessageJson));

    private static ProgramShip ReadShip(IGameItem item) => new((string)item.GetProperty(SerializationDependencies.IdProperty),
        (string)item.GetProperty(SerializationDependencies.KindProperty),
        (Property(item, "Position") as Vector)?.Coordinates.ToArray(),
        (Property(item, "Direction") as Angle)?.Numerator,
        Property(item, "Fuel") as int?, Property(item, "Ammo") as int?);

    private static object? Property(IGameItem item, string name)
    {
        try { return item.GetProperty(name); }
        catch (KeyNotFoundException) { return null; }
    }
}
