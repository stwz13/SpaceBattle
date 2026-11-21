namespace SpaceBattle.Lib;

internal sealed class AdvanceBattleProjectilesCommand : ICommand
{
    private readonly IGameItemStorage _items;
    private readonly IBattleShipClassifier _ships;
    private readonly IProjectileCollisionDetector _collisions;
    private readonly Func<ICommandContext, ICommand> _hit;

    public AdvanceBattleProjectilesCommand(IGameItemStorage items, IBattleShipClassifier ships, IProjectileCollisionDetector collisions,
        Func<ICommandContext, ICommand> hit)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(collisions);
        ArgumentNullException.ThrowIfNull(hit);
        _items = items;
        _ships = ships;
        _collisions = collisions;
        _hit = hit;
    }

    public void Execute()
    {
        var frame = _items.GetSnapshot();
        var ships = frame.Where(_ships.IsShip).ToArray();
        foreach (var projectile in frame.Where(item =>
                     BattleItemProperties.Read<string>(item, SerializationDependencies.KindProperty) == "Projectile"))
        {
            var owner = BattleItemProperties.Read<string>(projectile, "Owner");
            if (string.IsNullOrWhiteSpace(owner) || BattleItemProperties.IsDestroyed(projectile))
                continue;
            var start = (Vector)projectile.GetProperty("Position");
            var end = start + (Vector)projectile.GetProperty("Velocity");
            var target = _collisions.FirstHit(start, end, ships.Where(ship =>
                !BattleItemProperties.IsDestroyed(ship) &&
                BattleItemProperties.Read<string>(ship, "Owner") is string other && other != owner));
            if (target is null)
            {
                projectile.SetProperty("Position", end);
                continue;
            }

            _hit(new CommandContext(new Dictionary<string, IGameItem>
            {
                ["Source"] = projectile,
                ["Target"] = target
            })).Execute();
            target.SetProperty("Destroyed", true);
            _items.Remove((string)projectile.GetProperty(SerializationDependencies.IdProperty));
        }
    }
}
