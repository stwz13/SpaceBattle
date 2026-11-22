namespace SpaceBattle.Lib;

internal sealed class AdvanceBattleProjectilesCommand : ICommand
{
    private readonly IReadOnlyCollection<IGameItem> _items;
    private readonly IBattleShipClassifier _ships;
    private readonly IProjectileCollisionDetector _collisions;
    private readonly Func<IGameItem, IGameItem, ICommand> _hit;

    public AdvanceBattleProjectilesCommand(IReadOnlyCollection<IGameItem> items, IBattleShipClassifier ships, IProjectileCollisionDetector collisions,
        Func<IGameItem, IGameItem, ICommand> hit)
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
        var ships = _items.Where(_ships.IsShip).ToArray();
        foreach (var projectile in _items.Where(item =>
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

            _hit(projectile, target).Execute();
        }
    }
}
